namespace GA.Data.MongoDB.Services.Embeddings;

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

/// <summary>
///     Embedding service backed by a local ONNX model. Supports BERT-style tokenization,
///     configurable pooling, and optional L2 normalization so it can drop in wherever
///     <see cref="IEmbeddingService"/> is consumed.
/// </summary>
/// <remarks>
/// See https://github.com/onnx/models/tree/main/text/sentence_embeddings/bert-base-nli-mean-tokens | https://en.wikipedia.org/wiki/BERT_(language_model)
/// </remarks>
public sealed class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly ILogger<OnnxEmbeddingService> _logger;
    private readonly OnnxEmbeddingOptions _options;
    private readonly int _maxTokens;
    private readonly IOnnxSessionFactory _sessionFactory;
    private readonly IOnnxSession _session;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly long _padTokenId;
    private readonly long _clsTokenId;
    private readonly long _sepTokenId;
    private readonly long _unkTokenId;
    private bool _disposed;

    public OnnxEmbeddingService(string modelPath)
        : this(new OnnxEmbeddingOptions { ModelPath = modelPath })
    {
    }

    public OnnxEmbeddingService(OnnxEmbeddingOptions options, ILogger<OnnxEmbeddingService>? logger = null, IOnnxSessionFactory? sessionFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(_options);

        _logger = logger ?? NullLogger<OnnxEmbeddingService>.Instance;
        _maxTokens = Math.Max(_options.MaxTokens, 2);

        var vocabularyPath = ResolveVocabularyPath(_options);
        _vocabulary = LoadVocabulary(vocabularyPath);
        _options.VocabularyPath = vocabularyPath;

        _padTokenId = GetTokenId("[PAD]");
        _clsTokenId = GetTokenId("[CLS]");
        _sepTokenId = GetTokenId("[SEP]");
        _unkTokenId = GetTokenId("[UNK]");

        _sessionFactory = sessionFactory ?? DefaultOnnxSessionFactory.Instance;
        _session = _sessionFactory.Create(_options.ModelPath);
        _logger.LogInformation("ONNX embedding service initialized with model {Model} and vocab {Vocab}",
            _options.ModelPath,
            vocabularyPath);
    }

    public Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        ThrowIfDisposed();
        var result = GenerateEmbeddingInternal(text ?? string.Empty);
        return Task.FromResult(result);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.Dispose();
        _disposed = true;
        _logger.LogInformation("ONNX embedding session disposed");
    }

    private List<float> GenerateEmbeddingInternal(string text)
    {
        var (inputTensor, attentionTensor, attentionMask) = PrepareInputs(text);

        var inputValue = NamedOnnxValue.CreateFromTensor(_options.InputIdsNodeName, inputTensor);
        var attentionValue = NamedOnnxValue.CreateFromTensor(_options.AttentionMaskNodeName, attentionTensor);

        try
        {
            using var outputs = _session.Run(new[] { inputValue, attentionValue });

            var tokenEmbeddings = outputs
                .FirstOrDefault(output => output.Name == _options.OutputNodeName)?.AsTensor<float>()
                ?? throw new InvalidOperationException(
                    $"Output node '{_options.OutputNodeName}' was not found in the ONNX graph.");

            var pooled = PoolEmbeddings(tokenEmbeddings, attentionMask);

            if (_options.NormalizeEmbeddings)
            {
                NormalizeInPlace(pooled);
            }

            return [.. pooled];
        }
        finally
        {
            if (inputValue is IDisposable disposableInput)
            {
                disposableInput.Dispose();
            }

            if (attentionValue is IDisposable disposableAttention)
            {
                disposableAttention.Dispose();
            }
        }
    }

    private (DenseTensor<long> inputIds, DenseTensor<long> attentionMask, long[] attentionMaskRaw) PrepareInputs(string text)
    {
        var tokenIds = TokenizeToIds(text);

        if (tokenIds.Count > _maxTokens)
        {
            tokenIds = [.. tokenIds.Take(_maxTokens)];
            tokenIds[^1] = _sepTokenId;
        }

        var inputIds = new long[_maxTokens];
        var attentionMask = new long[_maxTokens];
        var validLength = Math.Min(tokenIds.Count, _maxTokens);

        for (var i = 0; i < validLength; i++)
        {
            inputIds[i] = tokenIds[i];
            attentionMask[i] = 1;
        }

        for (var i = validLength; i < _maxTokens; i++)
        {
            inputIds[i] = _padTokenId;
        }

        var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, _maxTokens });
        var attentionTensor = new DenseTensor<long>(attentionMask, new[] { 1, _maxTokens });

        return (inputTensor, attentionTensor, attentionMask);
    }

    private List<long> TokenizeToIds(string text)
    {
        var tokens = new List<long>(_maxTokens) { _clsTokenId };
        var contentTokens = BasicTokenize(text);

        foreach (var token in contentTokens)
        {
            foreach (var piece in WordPieceTokenize(token))
            {
                if (tokens.Count >= _maxTokens - 1)
                {
                    tokens.Add(_sepTokenId);
                    return tokens;
                }

                tokens.Add(piece);
            }
        }

        tokens.Add(_sepTokenId);
        return tokens;
    }

    private IEnumerable<string> BasicTokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var current = char.ToLowerInvariant(character);

            if (char.IsLetterOrDigit(current) || current == '\'' || current == '#')
            {
                builder.Append(current);
                continue;
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }

            if (!char.IsWhiteSpace(current))
            {
                yield return current.ToString();
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private IEnumerable<long> WordPieceTokenize(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            yield break;
        }

        if (_vocabulary.TryGetValue(token, out var fullTokenId))
        {
            yield return fullTokenId;
            yield break;
        }

        var start = 0;
        var isBad = false;
        var pieces = new List<long>();

        while (start < token.Length)
        {
            var end = token.Length;
            int? currentPieceId = null;

            while (start < end)
            {
                var substring = token.Substring(start, end - start);
                if (start > 0)
                {
                    substring = "##" + substring;
                }

                if (_vocabulary.TryGetValue(substring, out var pieceId))
                {
                    currentPieceId = pieceId;
                    break;
                }

                end--;
            }

            if (currentPieceId == null)
            {
                isBad = true;
                break;
            }

            pieces.Add(currentPieceId.Value);
            start = end;
        }

        if (isBad)
        {
            yield return _unkTokenId;
            yield break;
        }

        foreach (var piece in pieces)
        {
            yield return piece;
        }
    }

    private float[] PoolEmbeddings(Tensor<float> tokenEmbeddings, long[] attentionMask)
    {
        if (tokenEmbeddings.Dimensions.Length < 3)
        {
            var dimensionText = string.Join(
                ",",
                Enumerable.Range(0, tokenEmbeddings.Dimensions.Length)
                    .Select(i => tokenEmbeddings.Dimensions[i]));

            throw new InvalidOperationException(
                $"Unexpected embedding tensor shape: [{dimensionText}]");
        }

        var sequenceLength = tokenEmbeddings.Dimensions[tokenEmbeddings.Dimensions.Length - 2];
        var hiddenSize = tokenEmbeddings.Dimensions[^1];
        var pooled = new float[hiddenSize];

        if (_options.PoolingStrategy == OnnxEmbeddingPoolingStrategy.Cls)
        {
            for (var i = 0; i < hiddenSize; i++)
            {
                pooled[i] = tokenEmbeddings[0, 0, i];
            }

            return pooled;
        }

        var validTokens = 0;
        var limit = Math.Min(sequenceLength, attentionMask.Length);

        for (var tokenIndex = 0; tokenIndex < limit; tokenIndex++)
        {
            if (attentionMask[tokenIndex] == 0)
            {
                continue;
            }

            validTokens++;
            for (var hiddenIndex = 0; hiddenIndex < hiddenSize; hiddenIndex++)
            {
                pooled[hiddenIndex] += tokenEmbeddings[0, tokenIndex, hiddenIndex];
            }
        }

        if (validTokens > 0)
        {
            var scale = 1f / validTokens;
            for (var i = 0; i < pooled.Length; i++)
            {
                pooled[i] *= scale;
            }
        }

        return pooled;
    }

    private static void NormalizeInPlace(float[] vector)
    {
        double sumSquares = 0;
        for (var i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        if (sumSquares <= double.Epsilon)
        {
            return;
        }

        var scale = (float)(1d / Math.Sqrt(sumSquares));
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] *= scale;
        }
    }

    private long GetTokenId(string token)
    {
        if (_vocabulary.TryGetValue(token, out var id))
        {
            return id;
        }

        throw new InvalidOperationException(
            $"The vocabulary at '{_options.VocabularyPath}' is missing the required token '{token}'.");
    }

    private static string ResolveVocabularyPath(OnnxEmbeddingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.VocabularyPath))
        {
            if (!File.Exists(options.VocabularyPath))
            {
                throw new FileNotFoundException(
                    $"Vocabulary file '{options.VocabularyPath}' was not found.",
                    options.VocabularyPath);
            }

            return options.VocabularyPath;
        }

        var modelDirectory = Path.GetDirectoryName(options.ModelPath) ?? Directory.GetCurrentDirectory();
        var primaryCandidate = Path.Combine(modelDirectory, "vocab.txt");
        if (File.Exists(primaryCandidate))
        {
            return primaryCandidate;
        }

        var secondaryCandidate = Path.Combine(modelDirectory, "tokenizer", "vocab.txt");
        if (File.Exists(secondaryCandidate))
        {
            return secondaryCandidate;
        }

        throw new FileNotFoundException(
            "Unable to locate vocab.txt automatically. Provide OnnxEmbeddingOptions.VocabularyPath.");
    }

    private static Dictionary<string, int> LoadVocabulary(string path)
    {
        var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            var token = rawLine.Trim();
            if (token.Length == 0)
            {
                index++;
                continue;
            }

            if (!vocabulary.ContainsKey(token))
            {
                vocabulary[token] = index;
            }

            index++;
        }

        if (vocabulary.Count == 0)
        {
            throw new InvalidOperationException($"Vocabulary file '{path}' does not contain any tokens.");
        }

        return vocabulary;
    }

    private static void ValidateOptions(OnnxEmbeddingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            throw new ArgumentException("ModelPath must be provided for the ONNX embedding service.", nameof(options));
        }

        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException(
                $"ONNX model '{options.ModelPath}' was not found on disk.",
                options.ModelPath);
        }

        if (options.MaxTokens < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxTokens), "MaxTokens must be at least 2.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OnnxEmbeddingService));
        }
    }
}

public sealed class OnnxEmbeddingOptions
{
    public string ModelPath { get; set; } = string.Empty;
    public string? VocabularyPath { get; set; }
    public int MaxTokens { get; set; } = 256;
    public string InputIdsNodeName { get; set; } = "input_ids";
    public string AttentionMaskNodeName { get; set; } = "attention_mask";
    public string OutputNodeName { get; set; } = "last_hidden_state";
    public OnnxEmbeddingPoolingStrategy PoolingStrategy { get; set; } = OnnxEmbeddingPoolingStrategy.Mean;
    public bool NormalizeEmbeddings { get; set; } = true;
}

public enum OnnxEmbeddingPoolingStrategy
{
    Mean,
    Cls
}

/// <summary>
/// Abstraction over an ONNX inference session so tests can provide fakes.
/// </summary>
public interface IOnnxSession : IDisposable
{
    IDisposableReadOnlyCollection<NamedOnnxValue> Run(IReadOnlyCollection<NamedOnnxValue> inputs);
}

/// <summary>
/// Factory responsible for creating <see cref="IOnnxSession"/> instances.
/// </summary>
public interface IOnnxSessionFactory
{
    IOnnxSession Create(string modelPath);
}

internal sealed class DefaultOnnxSessionFactory : IOnnxSessionFactory
{
    public static DefaultOnnxSessionFactory Instance { get; } = new();

    private DefaultOnnxSessionFactory()
    {
    }

    public IOnnxSession Create(string modelPath) => new OnnxRuntimeSession(modelPath);

    private sealed class OnnxRuntimeSession : IOnnxSession
    {
        private readonly InferenceSession _session;

        public OnnxRuntimeSession(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        public IDisposableReadOnlyCollection<NamedOnnxValue> Run(IReadOnlyCollection<NamedOnnxValue> inputs)
            => new DisposableCollectionAdapter(_session.Run(inputs));

        public void Dispose() => _session.Dispose();

        private sealed class DisposableCollectionAdapter : IDisposableReadOnlyCollection<NamedOnnxValue>
        {
            private readonly IDisposableReadOnlyCollection<DisposableNamedOnnxValue> _inner;

            public DisposableCollectionAdapter(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> inner)
            {
                _inner = inner;
            }

            public int Count => _inner.Count;

            public NamedOnnxValue this[int index] => _inner[index];

            public void Dispose() => _inner.Dispose();

            public IEnumerator<NamedOnnxValue> GetEnumerator() => _inner.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
