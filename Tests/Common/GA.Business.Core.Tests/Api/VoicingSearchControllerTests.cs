namespace GA.Business.Core.Tests.Api;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GaApi.Controllers;
using GA.Business.Core.AI.Services.Embeddings;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard.Voicings.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

[TestFixture]
public class VoicingSearchControllerTests
{
    [Test]
    public async Task SemanticSearch_ReturnsEmbeddingBackedResults()
    {
        var document = new VoicingDocument
        {
            Id = "voicing-test",
            SearchableText = "test voicing",
            ChordName = "Cmaj7",
            SemanticTags = ["tag"],
            YamlAnalysis = "yaml",
            Diagram = "x-3-2-0-1-0",
            MidiNotes = [60],
            PitchClassSet = "{0,4,7,11}",
            IntervalClassVector = "<0 0 0 0 0 0>",
            PrimeFormId = "pf",
            TranslationOffset = 0,
            Difficulty = "Intermediate",
            Position = "Open Position",
            ModeName = "Ionian",
            ModalFamily = "Major Scale Family",
            MinFret = 0,
            MaxFret = 3,
            HandStretch = 5,
            BarreRequired = false
        };

        var indexingService = new VoicingIndexingService();
        SeedIndexedDocuments(indexingService, [document]);

        var strategy = new TestSearchStrategy(document);
        var enhancedService = new EnhancedVoicingSearchService(indexingService, strategy);
        var embeddingService = new TestEmbeddingService();
        var embeddingCache = new TestEmbeddingCache(embeddingService);

        var controller = new VoicingSearchController(
            enhancedService,
            embeddingService,
            embeddingCache,
            NullLogger<VoicingSearchController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SemanticSearch("test query");
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        Assert.That(okResult.Value, Is.InstanceOf<List<VoicingSearchResult>>());
        var payload = (List<VoicingSearchResult>)okResult.Value!;

        Assert.That(payload.Count, Is.EqualTo(1));
        Assert.That(payload[0].Document.Id, Is.EqualTo(document.Id));
        Assert.That(embeddingCache.RequestCount, Is.GreaterThan(0));
        Assert.That(strategy.Initialized, Is.True);
    }

    private static void SeedIndexedDocuments(VoicingIndexingService service, IEnumerable<VoicingDocument> documents)
    {
        var field = typeof(VoicingIndexingService).GetField("_indexedDocuments", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("Unable to set indexed documents");
        }

        field.SetValue(service, new List<VoicingDocument>(documents));
    }

    private class TestSearchStrategy : IVoicingSearchStrategy
    {
        private List<VoicingEmbedding> _voicings = [];
        public bool Initialized { get; private set; }
        public VoicingDocument Document { get; }

        public TestSearchStrategy(VoicingDocument document)
        {
            Document = document;
        }

        public string Name => "TestStrategy";
        public bool IsAvailable => true;
        public VoicingSearchPerformance Performance => new(TimeSpan.FromMilliseconds(1), 1, false, false);

        public Task InitializeAsync(IEnumerable<VoicingEmbedding> voicings)
        {
            _voicings = new List<VoicingEmbedding>(voicings);
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task<List<VoicingSearchResult>> SemanticSearchAsync(double[] queryEmbedding, int limit = 10)
        {
            return Task.FromResult(
                Enumerable.Range(0, limit)
                    .Select(_ => new VoicingSearchResult(Document, 0.5, "test query"))
                    .ToList());
        }

        public Task<List<VoicingSearchResult>> FindSimilarVoicingsAsync(string voicingId, int limit = 10)
        {
            return Task.FromResult(new List<VoicingSearchResult>());
        }

        public Task<List<VoicingSearchResult>> HybridSearchAsync(double[] queryEmbedding, VoicingSearchFilters filters, int limit = 10)
        {
            return Task.FromResult(new List<VoicingSearchResult>());
        }

        public VoicingSearchStats GetStats() => new(0, 0, TimeSpan.Zero, 0);
    }

    private class TestEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { 1f, 2f, 3f });
        }
    }

    private class TestEmbeddingCache : IVoicingEmbeddingCache
    {
        private readonly IEmbeddingService _embeddingService;
        public int RequestCount;

        public TestEmbeddingCache(IEmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
        }

        public async Task<double[]> GetOrCreateAsync(string text, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref RequestCount);
            var floats = await _embeddingService.GenerateEmbeddingAsync(text, cancellationToken);
            return [.. floats.Select(f => (double)f)];
        }
    }
}
