namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Naturalness;
using GA.Domain.Core.Theory.Tabs;
using GA.Domain.Repositories;

/// <summary>
///     The naturalness CSV is read back with a comma separator (TrainNaturalnessModelCommand), so its numbers must not
///     depend on the machine's culture.
/// </summary>
[TestFixture]
public class NaturalnessTrainingDataGeneratorTests
{
    // The tablature GenerateNaturalnessDataCommand seeds when the corpus has none
    private const string Tab = """
        Tuning: E A D G B E
        Simple Chords
            C   Am  F   G
        e|--0---0---1---3---|
        B|--1---1---1---0---|
        G|--0---2---2---0---|
        D|--2---2---3---0---|
        A|--3---0---3---2---|
        E|----------1---3---|
        """;

    [Test]
    [SetCulture("fr-FR")]
    public async Task GenerateCsvAsync_WritesSixValuesPerRow_UnderACommaDecimalCulture()
    {
        var csv = await new NaturalnessTrainingDataGenerator(new OneTabRepository(Tab)).GenerateCsvAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.GreaterThan(1), "a header and at least one row");
            Assert.That(lines.Select(line => line.Split(',').Length), Has.All.EqualTo(6));
        });
    }

    private sealed class OneTabRepository(string content) : ITabCorpusRepository
    {
        private readonly TabCorpusItem _item = new()
        {
            Id = "test-tab",
            SourceId = "test",
            ExternalId = "test-tab",
            Content = content,
            Format = "ASCII"
        };

        public Task SaveAsync(TabCorpusItem item) => Task.CompletedTask;
        public Task<TabCorpusItem?> GetByIdAsync(string id) => Task.FromResult<TabCorpusItem?>(_item);
        public Task<IEnumerable<TabCorpusItem>> GetAllAsync() => Task.FromResult<IEnumerable<TabCorpusItem>>([_item]);
        public Task<bool> ExistsAsync(string id) => Task.FromResult(true);
        public Task<long> CountAsync() => Task.FromResult(1L);
    }
}
