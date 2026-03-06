namespace GenerateNatData.Phase2;

using System.Diagnostics;
using GA.Business.ML.Embeddings;
using GenerateNatData.Output;
using GenerateNatData.Phase1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Orchestrates the two-step embedding pipeline:
///     1. GPU kernel for STRUCTURE (dims 6-29) + MORPHOLOGY (dims 30-53)
///     2. CPU EmbeddingComputer for remaining dims (EXTENSIONS, SPECTRAL, etc.)
///     Then writes voicings.bin and voicings-meta.bin.
/// </summary>
public sealed class BatchEmbedder
{
    private readonly ILogger<BatchEmbedder> _logger;
    private readonly IOpticKKernel? _kernelOverride;

    public BatchEmbedder(ILogger<BatchEmbedder>? logger = null, IOpticKKernel? kernelOverride = null)
    {
        _logger = logger ?? NullLogger<BatchEmbedder>.Instance;
        _kernelOverride = kernelOverride;
    }

    /// <summary>
    ///     Runs the full embedding pass from a scratch file and writes output files.
    /// </summary>
    /// <param name="scratchPath">Path to Phase 1 scratch binary.</param>
    /// <param name="outputDir">Directory to write voicings.bin and voicings-meta.bin.</param>
    /// <param name="config">Constraint config (used for output file header).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task EmbedAsync(
        string scratchPath,
        string outputDir,
        ConstraintConfig config,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        Directory.CreateDirectory(outputDir);

        // Read scratch header
        var (n, stringCount, _) = VoicingEnumerator.ReadHeader(scratchPath);
        _logger.LogInformation("[Phase 2] Embedding {N:N0} voicings from {Path}", n, scratchPath);

        // Load all fret records into a flat sbyte array for GPU upload
        var allFrets = new sbyte[n * stringCount];
        var allStartingFrets = new byte[n];
        var idx = 0;
        foreach (var (frets, startingFret) in VoicingEnumerator.ReadRecords(scratchPath, stringCount))
        {
            for (var s = 0; s < stringCount; s++)
                allFrets[idx * stringCount + s] = frets[s];
            allStartingFrets[idx] = startingFret;
            idx++;
            if (idx > n) break; // guard against malformed files
        }

        _logger.LogInformation("[Phase 2] Loaded {N:N0} voicings in {Elapsed:F1}s", n, sw.Elapsed.TotalSeconds);

        // GPU pass: compute STRUCTURE + MORPHOLOGY (dims 6-53) for all voicings in parallel
        float[] gpuOutput;
        try
        {
            IOpticKKernel kernel = _kernelOverride ?? new OpticKGpuKernel(_logger as ILogger<OpticKGpuKernel>);
            var ownKernel = _kernelOverride is null;
            try
            {
                _logger.LogInformation("[Phase 2] GPU dispatch for dims 6-53 ...");
                gpuOutput = kernel.ComputeBatch(allFrets, EmbeddingComputer.StandardTuningMidi);
                _logger.LogInformation("[Phase 2] GPU pass done in {Elapsed:F1}s", sw.Elapsed.TotalSeconds);
            }
            finally
            {
                if (ownKernel) kernel.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Phase 2] GPU kernel failed; falling back to CPU for all dims");
            gpuOutput = [];
        }

        // CPU pass: build full 228-dim vectors, injecting GPU results for dims 6-53
        var embeddings = new float[n * EmbeddingSchema.TotalDimension];
        var metas = new VoicingMetaRecord[n];

        Parallel.For(0, n, i =>
        {
            ct.ThrowIfCancellationRequested();

            var fretSlice = allFrets.AsSpan(i * stringCount, stringCount);
            var embedding = EmbeddingComputer.Compute(
                new ReadOnlySpan<sbyte>(allFrets, i * stringCount, stringCount),
                EmbeddingComputer.StandardTuningMidi);

            // Overwrite dims 6-53 with GPU results (higher quality batch computation)
            if (gpuOutput.Length >= (i + 1) * OpticKGpuKernel.OutputDimsPerVoicing)
            {
                var gpuSlice = gpuOutput.AsSpan(
                    i * OpticKGpuKernel.OutputDimsPerVoicing,
                    OpticKGpuKernel.OutputDimsPerVoicing);
                gpuSlice.CopyTo(embedding.AsSpan(EmbeddingSchema.StructureOffset, OpticKGpuKernel.OutputDimsPerVoicing));
            }

            // Copy embedding into flat output array
            embedding.CopyTo(embeddings, i * EmbeddingSchema.TotalDimension);

            // Build meta record
            metas[i] = VoicingMetaRecord.FromFrets(fretSlice, allStartingFrets[i], stringCount);
        });

        _logger.LogInformation("[Phase 2] CPU pass done in {Elapsed:F1}s", sw.Elapsed.TotalSeconds);

        // Write output files
        var embeddingsPath = Path.Combine(outputDir, "voicings.bin");
        var metasPath = Path.Combine(outputDir, "voicings-meta.bin");

        await BinaryVoicingWriter.WriteEmbeddingsAsync(
            embeddings, n, EmbeddingSchema.TotalDimension, config, embeddingsPath, ct);
        await BinaryVoicingWriter.WriteMetasAsync(metas, metasPath, ct);

        _logger.LogInformation(
            "[Phase 2] Done. voicings.bin={EmbSize:N0} bytes, voicings-meta.bin={MetaSize:N0} bytes ({Elapsed:F1}s)",
            new FileInfo(embeddingsPath).Length,
            new FileInfo(metasPath).Length,
            sw.Elapsed.TotalSeconds);
    }
}
