namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Helpers;

/// <summary>
/// Clients rebuild the answer by concatenating SSE chunks verbatim, so the chunker must keep the
/// whitespace between sentences. The 2026-09-28 public tracer found it consumed that whitespace:
/// the per-chord arpeggio list rendered as one bullet ("…i chords).- **F** → …") and ordinary
/// sentences were glued ("…the note Q.To create…").
/// </summary>
[TestFixture]
public class SseChunkerTests
{
    private const string ProgressionAnswer =
        "Over **Am – F – C – G**, for each chord:\n\n" +
        "- **Am** → arpeggio **Am**, play **A Aeolian (natural minor)** (diatonic minor — fits most i chords).\n" +
        "- **F** → arpeggio **F**, play **F Ionian (major)** (diatonic, includes the 7 — careful on the IV chord).\n" +
        "\n" +
        "Each arpeggio spells the chord tones; the scale adds the passing notes for lines between them.\n";

    [TestCase(ProgressionAnswer)]
    [TestCase("First sentence. Second one! Third? Done.")]
    [TestCase("Ends with spaces.   Next.\t\tLast.")]
    public void Concatenated_chunks_reproduce_the_answer_exactly(string answer)
    {
        var chunks = SseChunker.SplitIntoChunks(answer).ToList();

        Assert.That(string.Concat(chunks), Is.EqualTo(answer));
    }

    [Test]
    public void Splits_at_sentence_boundaries_for_progressive_rendering()
    {
        var chunks = SseChunker.SplitIntoChunks("One. Two! Three?").ToList();

        Assert.That(chunks, Is.EqualTo(new[] { "One. ", "Two! ", "Three?" }));
    }

    [Test]
    public void List_items_keep_their_line_breaks()
    {
        var chunks = SseChunker.SplitIntoChunks(ProgressionAnswer).ToList();

        Assert.That(chunks, Has.Some.EndsWith("fits most i chords).\n"));
        Assert.That(chunks, Has.Some.StartsWith("- **F** →"));
        Assert.That(chunks, Has.None.Matches(@"^\s*$"), "controllers skip whitespace-only chunks");
    }

    [TestCase("")]
    [TestCase("   \n\t")]
    public void Whitespace_only_input_yields_nothing(string answer) =>
        Assert.That(SseChunker.SplitIntoChunks(answer), Is.Empty);
}
