namespace GA.Business.Core.Tests.Configuration;

using GA.Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
///     The musical knowledge cache stores each list of strings as one joined column. The read side used
///     <c>StringSplitOptions.RemoveEmptyEntries</c>, so an empty element was lost: three alternate names
///     saved, two read back, and parallel lists (Roman numerals and chords) fell out of step.
/// </summary>
[TestFixture]
public class MusicalKnowledgeDbContextConverterTests
{
    private static IEnumerable<IProperty> StringListProperties()
    {
        // Building the model needs a provider but never opens the connection.
        var options = new DbContextOptionsBuilder<MusicalKnowledgeDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var context = new MusicalKnowledgeDbContext(options);
        return context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(List<string>) && p.GetValueConverter() is not null)
            .ToList();
    }

    private static IEnumerable<TestCaseData> StringListCases() =>
        StringListProperties().Select(p => new TestCaseData(p).SetArgDisplayNames($"{p.DeclaringType.ClrType.Name}.{p.Name}"));

    [Test]
    public void StringListConverters_AreFound() =>
        Assert.That(StringListProperties().Select(p => p.Name), Does.Contain("AlternateNames"));

    [TestCaseSource(nameof(StringListCases))]
    public void StringListConverter_RoundTripsEmptyElements(IProperty property)
    {
        var converter = property.GetValueConverter()!;

        Assert.Multiple(() =>
        {
            foreach (var list in new List<string>[] { ["Cmaj7", "", "CΔ7"], ["", "x"], ["x", ""], ["x"], [] })
            {
                var stored = converter.ConvertToProvider(list);
                var readBack = (List<string>)converter.ConvertFromProvider(stored)!;
                Assert.That(readBack, Is.EqualTo(list), $"{property.DeclaringType.ClrType.Name}.{property.Name} stored as '{stored}'");
            }
        });
    }
}
