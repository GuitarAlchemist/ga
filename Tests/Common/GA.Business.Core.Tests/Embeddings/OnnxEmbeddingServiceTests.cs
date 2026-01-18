namespace GA.Business.Core.Tests.Embeddings;

using System.Collections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ML.Abstractions;
using ML.Text.Onnx;
using NUnit.Framework;

[TestFixture]
public class OnnxEmbeddingServiceTests
{
    [Test]
    public async Task ShouldNormalizeMeanPooledEmbeddings()
    {
        using var harness = new OnnxServiceTestHarness();

        var embedding = await harness.Service.GenerateEmbeddingAsync("alpha beta");

        Assert.That(embedding, Has.Count.EqualTo(harness.SessionFactory.HiddenSize));
        var session = harness.SessionFactory.LastSession!;
        var mask = session.LastAttentionMask!;
        var expected = Normalize(ComputeMeanVector(mask, harness.SessionFactory.HiddenSize));

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.That(embedding[i], Is.EqualTo(expected[i]).Within(1e-5f));
        }
    }

    [Test]
    public async Task ShouldRespectClsPoolingStrategy()
    {
        using var harness = new OnnxServiceTestHarness(OnnxEmbeddingPoolingStrategy.Cls);

        var embedding = await harness.Service.GenerateEmbeddingAsync("alpha beta");

        var expected = Normalize([1, 2]);
        Assert.That(embedding[0], Is.EqualTo(expected[0]).Within(1e-5f));
        Assert.That(embedding[1], Is.EqualTo(expected[1]).Within(1e-5f));
    }

    [Test]
    public async Task ShouldUseUnknownTokenWhenNotInVocabulary()
    {
        using var harness = new OnnxServiceTestHarness();

        await harness.Service.GenerateEmbeddingAsync("mystery");

        var session = harness.SessionFactory.LastSession!;
        Assert.That(session.LastInputIds, Is.Not.Null, "Input IDs were not captured");
        Assert.That(session.LastInputIds![1], Is.EqualTo(harness.UnknownTokenId));
    }

    private static double[] ComputeMeanVector(long[] attentionMask, int hiddenSize)
    {
        var sums = new double[hiddenSize];
        var count = 0;

        for (var token = 0; token < attentionMask.Length; token++)
        {
            if (attentionMask[token] == 0)
            {
                continue;
            }

            count++;
            var baseValue = token + 1;
            for (var dim = 0; dim < hiddenSize; dim++)
            {
                sums[dim] += baseValue * (dim + 1);
            }
        }

        if (count == 0)
        {
            return sums;
        }

        for (var i = 0; i < hiddenSize; i++)
        {
            sums[i] /= count;
        }

        return sums;
    }

    private static double[] Normalize(double[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude <= double.Epsilon)
        {
            return vector;
        }

        var normalized = new double[vector.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }

        return normalized;
    }

    private sealed class OnnxServiceTestHarness : IDisposable
    {
        private readonly string _tempDirectory;

        public OnnxServiceTestHarness(OnnxEmbeddingPoolingStrategy poolingStrategy = OnnxEmbeddingPoolingStrategy.Mean)
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"onnx-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDirectory);

            ModelPath = Path.Combine(_tempDirectory, "model.onnx");
            File.WriteAllBytes(ModelPath, Array.Empty<byte>());

            VocabularyPath = Path.Combine(_tempDirectory, "vocab.txt");
            File.WriteAllLines(VocabularyPath, [
                "[PAD]",
                "[CLS]",
                "[SEP]",
                "[UNK]",
                "alpha",
                "beta",
                "hello"
            ]);

            SessionFactory = new FakeOnnxSessionFactory(hiddenSize: 2);
            var options = new OnnxEmbeddingOptions
            {
                ModelPath = ModelPath,
                VocabularyPath = VocabularyPath,
                MaxTokens = 8,
                PoolingStrategy = poolingStrategy,
                NormalizeEmbeddings = true
            };

            Service = new OnnxEmbeddingService(options, NullLogger<OnnxEmbeddingService>.Instance, SessionFactory);
        }

        public string ModelPath { get; }
        public string VocabularyPath { get; }
        public long UnknownTokenId => 3;
        public OnnxEmbeddingService Service { get; }
        public FakeOnnxSessionFactory SessionFactory { get; }

        public void Dispose()
        {
            Service.Dispose();
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class FakeOnnxSessionFactory(int hiddenSize) : IOnnxSessionFactory
    {
        public int HiddenSize => hiddenSize;
        public FakeOnnxSession? LastSession { get; private set; }

        public IOnnxSession Create(string modelPath)
        {
            var session = new FakeOnnxSession(hiddenSize);
            LastSession = session;
            return session;
        }
    }

    private sealed class FakeOnnxSession(int hiddenSize) : IOnnxSession
    {
        public long[]? LastInputIds { get; private set; }
        public long[]? LastAttentionMask { get; private set; }

        public IDisposableReadOnlyCollection<NamedOnnxValue> Run(IReadOnlyCollection<NamedOnnxValue> inputs)
        {
            var materialized = inputs.ToList();
            var inputIds = materialized.Single(v => v.Name == "input_ids").AsTensor<long>().ToArray();
            var attentionMask = materialized.Single(v => v.Name == "attention_mask").AsTensor<long>().ToArray();

            LastInputIds = inputIds;
            LastAttentionMask = attentionMask;

            var tensor = BuildTensor(attentionMask, hiddenSize);
            var value = NamedOnnxValue.CreateFromTensor("last_hidden_state", tensor);
            return new FakeResultCollection(value);
        }

        public void Dispose()
        {
        }

        private static DenseTensor<float> BuildTensor(long[] attentionMask, int hiddenSize)
        {
            var dims = new[] { 1, attentionMask.Length, hiddenSize };
            var data = new float[attentionMask.Length * hiddenSize];

            for (var token = 0; token < attentionMask.Length; token++)
            {
                var weight = attentionMask[token] == 0 ? 0 : token + 1;
                for (var dim = 0; dim < hiddenSize; dim++)
                {
                    data[token * hiddenSize + dim] = weight * (dim + 1);
                }
            }

            return new DenseTensor<float>(data, dims);
        }
    }

    private sealed class FakeResultCollection(params NamedOnnxValue[] values) : IDisposableReadOnlyCollection<NamedOnnxValue>
    {
        private readonly IReadOnlyList<NamedOnnxValue> _values = values;

        public int Count => _values.Count;
        public NamedOnnxValue this[int index] => _values[index];

        public void Dispose()
        {
            foreach (var value in _values)
            {
                if (value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        public IEnumerator<NamedOnnxValue> GetEnumerator() => _values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
