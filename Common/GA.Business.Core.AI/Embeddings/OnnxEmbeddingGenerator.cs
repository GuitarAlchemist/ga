namespace GA.Business.Core.AI.Embeddings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics.Tensors;
using System.Threading.Tasks;
using GA.Business.Core.AI;
using GA.Business.Core.Fretboard.Voicings.Search;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

/// <summary>
/// Generates semantic embeddings using a local ONNX model (e.g. all-MiniLM-L6-v2)
/// </summary>
public class OnnxEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly BertTokenizer? _tokenizer;
    private readonly bool _isAvailable;
    
    // Model specific constants (all-MiniLM-L6-v2)
    private const int EmbeddingDim = 384;
    private const int MaxSequenceLength = 128;
    
    public int Dimension => EmbeddingDim;

    public OnnxEmbeddingGenerator(string modelPath, string vocabPath)
    {
        if (File.Exists(modelPath) && File.Exists(vocabPath))
        {
            try
            {
                var options = new SessionOptions();
                // options.AppendExecutionProvider_CUDA(); 
                
                _session = new InferenceSession(modelPath, options);
                
                // Initialize WordPiece tokenizer (standard for BERT models)
                _tokenizer = BertTokenizer.Create(vocabPath);
                
                _isAvailable = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load ONNX model or vocab: {ex.Message}");
                _isAvailable = false;
            }
        }
        else 
        {
             _isAvailable = false;
        }
    }
    
    // Fallback constructor for when resources aren't ready
    public OnnxEmbeddingGenerator() : this("", "") { }

    public Task<double[]> GenerateEmbeddingAsync(VoicingDocument document)
    {
        if (!_isAvailable) return Task.FromResult(new double[Dimension]);

        // Construct semantic text representation
        var text = $"{document.ChordName} {document.VoicingType} voicing. {string.Join(" ", document.SemanticTags ?? [])} {document.TexturalDescription}";
        
        return GenerateEmbeddingAsync(text);
    }

    public Task<double[]> GenerateEmbeddingAsync(string text)
    {
        if (!_isAvailable || _session == null || _tokenizer == null) 
            return Task.FromResult(new double[Dimension]);

        return Task.Run(() =>
        {
            try
            {
                // 1. Tokenize
                var tokens = _tokenizer.EncodeToIds(text).Take(MaxSequenceLength).Select(x => (long)x).ToArray();
                
                // Pad if necessary (though ONNX Runtime often handles variable length if configured, 
                // but BERT usually expects fixed or masked inputs. 
                // Let's create the inputs expected by standard BERT models: input_ids, attention_mask, token_type_ids
                
                var inputIds = new long[MaxSequenceLength];
                var attentionMask = new long[MaxSequenceLength];
                var tokenTypeIds = new long[MaxSequenceLength]; // All 0 for single sentence

                for (int i = 0; i < tokens.Length; i++)
                {
                    inputIds[i] = tokens[i];
                    attentionMask[i] = 1;
                }
                
                // 2. Create Tensors
                var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, MaxSequenceLength });
                var inputMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, MaxSequenceLength });
                var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, MaxSequenceLength });
                
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                    NamedOnnxValue.CreateFromTensor("attention_mask", inputMaskTensor),
                    NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
                };

                // 3. Run Inference
                using var results = _session.Run(inputs);
                
                // 4. Extract Output
                // Output is usually "last_hidden_state" or "pooler_output" depending on export.
                // For sentence-transformers, we often want Mean Pooling of the last_hidden_state.
                // Let's assume standard export with 'last_hidden_state' [1, 128, 384]
                
                var output = results.First().AsTensor<float>(); // [1, 128, 384]
                
                // Mean Pooling (average of non-padded tokens)
                var validCount = tokens.Length;
                var embedding = new double[Dimension];
                
                for (int i = 0; i < validCount; i++) // Iterate tokens
                {
                   for(int j=0; j<Dimension; j++) // Iterate dims
                   {
                        // Get value at [0, i, j]
                        var val = output[0, i, j];
                        embedding[j] += val;
                   }
                }
                
                // Average
                for(int j=0; j<Dimension; j++)
                {
                    embedding[j] /= validCount;
                }
                
                // Normalize (Cosine Similarity requires unit vectors)
                 var norm = Math.Sqrt(embedding.Sum(x => x * x));
                 if (norm > 0)
                 {
                     for(int j=0; j<Dimension; j++) embedding[j] /= norm;
                 }

                return embedding;
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Inference failed: {ex.Message}");
                return new double[Dimension];
            }
        });
    }

    public Task<double[][]> GenerateBatchEmbeddingsAsync(IEnumerable<VoicingDocument> documents)
    {
        if (!_isAvailable || _session == null || _tokenizer == null) 
            return Task.FromResult(documents.Select(_ => new double[Dimension]).ToArray());

        var docs = documents.ToList();
        if (docs.Count == 0) return Task.FromResult(Array.Empty<double[]>());

        return Task.Run(() =>
        {
            try
            {
                var inputIds = new long[docs.Count * MaxSequenceLength];
                var attentionMask = new long[docs.Count * MaxSequenceLength];
                var tokenTypeIds = new long[docs.Count * MaxSequenceLength]; // All 0

                for (int i = 0; i < docs.Count; i++)
                {
                    var doc = docs[i];
                    var text = $"{doc.ChordName} {doc.VoicingType} voicing. {string.Join(" ", doc.SemanticTags ?? [])} {doc.TexturalDescription}";
                    
                    var tokens = _tokenizer.EncodeToIds(text).Take(MaxSequenceLength).Select(x => (long)x).ToArray();
                    
                    // Copy to batch array at offset i * MaxSequenceLength
                    var offset = i * MaxSequenceLength;
                    for (int j = 0; j < MaxSequenceLength; j++)
                    {
                        var globalIdx = offset + j;
                        if (j < tokens.Length)
                        {
                            inputIds[globalIdx] = tokens[j];
                            attentionMask[globalIdx] = 1;
                        }
                        else
                        {
                            // Pad
                            inputIds[globalIdx] = 0;
                            attentionMask[globalIdx] = 0;
                        }
                    }
                }

                // Create Batched Tensors [Batch, Seq]
                var batchShape = new[] { docs.Count, MaxSequenceLength };
                var inputIdsTensor = new DenseTensor<long>(inputIds, batchShape);
                var inputMaskTensor = new DenseTensor<long>(attentionMask, batchShape);
                var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, batchShape);

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                    NamedOnnxValue.CreateFromTensor("attention_mask", inputMaskTensor),
                    NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
                };

                using var results = _session.Run(inputs);
                var output = results.First().AsTensor<float>(); // [Batch, 128, 384]
                
                var embeddings = new double[docs.Count][];
                
                for (int i = 0; i < docs.Count; i++)
                {
                    var embedding = new double[Dimension];
                    // Re-calculate valid count for this specific doc
                     var validCount = 0;
                     var offset = i * MaxSequenceLength;
                     for(int k=0; k<MaxSequenceLength; k++)
                     {
                         if(attentionMask[offset + k] == 1) validCount++;
                     }

                     if (validCount == 0) validCount = 1; // Prevent div/0

                    // Mean Pooling
                    for (int j = 0; j < validCount; j++)
                    {
                        for (int dim = 0; dim < Dimension; dim++)
                        {
                            embedding[dim] += output[i, j, dim];
                        }
                    }
                    
                    // Average
                    for (int dim = 0; dim < Dimension; dim++) embedding[dim] /= validCount;

                    // Normalize
                    var norm = Math.Sqrt(embedding.Sum(x => x * x));
                    if (norm > 0)
                    {
                        for (int dim = 0; dim < Dimension; dim++) embedding[dim] /= norm;
                    }
                    
                    embeddings[i] = embedding;
                }

                return embeddings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Batch Inference failed: {ex.Message}");
                return docs.Select(_ => new double[Dimension]).ToArray();
            }
        });
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
