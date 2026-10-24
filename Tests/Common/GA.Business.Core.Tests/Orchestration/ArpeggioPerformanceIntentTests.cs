namespace GA.Business.Core.Tests.Orchestration;

using System.Net.Http;
using GA.Business.Core.Orchestration.Clients;
using GA.Business.Core.Orchestration.PerformanceIntents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Regression tests for the ga#589 arpeggio structured-output tracer. Drives the exact
/// #567 failure inputs through the deserialise → validate → render seam
/// (<see cref="ArpeggioIntentService.InterpretModelJson"/>) — the whole slice minus the HTTP
/// hop to Ollama — and asserts the theory engine refuses structurally wrong advice instead of
/// rendering it. The happy path proves a valid intent still gets through.
/// </summary>
[TestFixture]
public class ArpeggioPerformanceIntentTests
{
    // The service ctor needs an OllamaGenerateClient, but InterpretModelJson never calls it, so a
    // client over a stub factory is enough — no network, deterministic.
    private static ArpeggioIntentService MakeService()
    {
        var httpFactory = new Mock<IHttpClientFactory>().Object;
        var config = new ConfigurationBuilder().Build();
        var ollama = new OllamaGenerateClient(httpFactory, config);
        return new ArpeggioIntentService(ollama, new PerformanceIntentValidator(), NullLogger<ArpeggioIntentService>.Instance);
    }

    // ── #567 case 1: 'Amm7' concatenation artefact must be refused ──────────────────

    [Test]
    public void Interpret_Amm7ConcatenationArtefact_IsRefused()
    {
        // What the old arpeggio tool emitted: "Am" + "m7" = "Amm7".
        const string modelJson =
            """
            {
              "chord": "Am",
              "key": "C major",
              "degrees": [{ "chord": "Am", "roman": "vi" }],
              "suggested_arpeggios": [{ "chord": "Am", "arpeggio": "Amm7", "mode": "Aeolian (minor)" }]
            }
            """;

        var answer = MakeService().InterpretModelJson(modelJson);

        Assert.That(answer.Answered, Is.False, "an unparseable arpeggio symbol must not be answered");
        Assert.That(answer.Problems, Has.Some.Contains("Amm7"));
        Assert.That(answer.Text, Does.StartWith("I can't give validated arpeggio advice"),
            "the response is a deterministic refusal, not rendered advice");
    }

    // ── #567 case 2: key-blind mapping of a borrowed chord must be refused ───────────

    [Test]
    public void Interpret_BorrowedChordMappedToDiatonicMode_IsRefused()
    {
        // An A *major* chord in C major (borrowed / secondary). The old key-blind code matched it
        // to degree vi → Aeolian, putting a natural C against the chord's C#.
        const string modelJson =
            """
            {
              "chord": "A",
              "key": "C major",
              "degrees": [{ "chord": "A", "roman": "vi" }],
              "suggested_arpeggios": [{ "chord": "A", "arpeggio": "A", "mode": "Aeolian (minor)" }]
            }
            """;

        var answer = MakeService().InterpretModelJson(modelJson);

        Assert.That(answer.Answered, Is.False, "a borrowed chord must not be forced onto a diatonic mode");
        Assert.That(answer.Problems, Has.Some.Contains("not diatonic"));
    }

    // ── Happy path: a correct, diatonic intent renders ──────────────────────────────

    [Test]
    public void Interpret_ValidDiatonicIntent_IsAnswered()
    {
        // The correct advice for the #567 progression Am F C G in C major.
        const string modelJson =
            """
            {
              "chord": "Am",
              "key": "C major",
              "degrees": [
                { "chord": "Am", "roman": "vi" }, { "chord": "F", "roman": "IV" },
                { "chord": "C", "roman": "I" },   { "chord": "G", "roman": "V" }
              ],
              "suggested_arpeggios": [
                { "chord": "Am", "arpeggio": "Am7",   "mode": "Aeolian (minor)" },
                { "chord": "F",  "arpeggio": "Fmaj7", "mode": "Lydian" },
                { "chord": "C",  "arpeggio": "Cmaj7", "mode": "Ionian (major)" },
                { "chord": "G",  "arpeggio": "G7",    "mode": "Mixolydian" }
              ]
            }
            """;

        var answer = MakeService().InterpretModelJson(modelJson);

        Assert.That(answer.Answered, Is.True, "a well-formed diatonic intent should validate");
        Assert.That(answer.Problems, Is.Empty);
        Assert.That(answer.Text, Does.Contain("Am7").And.Contain("Cmaj7"));
    }

    // ── Non-JSON / empty model output degrades to a refusal, never a throw ───────────

    [Test]
    public void Interpret_NonJsonModelOutput_IsRefusedNotThrown()
    {
        var answer = MakeService().InterpretModelJson("Sure! Over Am try the A Aeolian arpeggio...");
        Assert.That(answer.Answered, Is.False);
        Assert.That(answer.Problems, Is.Not.Empty);
    }

    // ── Validator unit checks (deterministic ground truth) ──────────────────────────

    [Test]
    public void Validator_RejectsNullAndEmptyIntents()
    {
        var validator = new PerformanceIntentValidator();
        Assert.That(validator.Validate(null).IsValid, Is.False);
        Assert.That(validator.Validate(new PerformanceIntent { Key = "C major" }).IsValid, Is.False,
            "no suggestions → invalid");
    }

    [Test]
    public void Validator_RejectsArpeggioNotRootedOnChord()
    {
        var validator = new PerformanceIntentValidator();
        var intent = new PerformanceIntent
        {
            Chord = "C",
            Key = "C major",
            SuggestedArpeggios = [new ArpeggioSuggestion { Chord = "C", Arpeggio = "Dm7", Mode = "Ionian (major)" }],
        };
        var result = validator.Validate(intent);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Problems, Has.Some.Contains("not rooted on C"));
    }
}
