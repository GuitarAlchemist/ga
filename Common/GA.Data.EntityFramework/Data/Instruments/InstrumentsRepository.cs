namespace GA.Data.EntityFramework.Data.Instruments;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Business.Config;
using JetBrains.Annotations;

[PublicAPI]
public sealed class InstrumentsRepository
{
    public static readonly InstrumentsRepository Instance = new();

    private readonly SortedDictionary<string, InstrumentInfo> _instruments = [];

    public InstrumentsRepository()
    {
        PopulateInstruments();
    }

    /// <summary>
    ///     Gets the instrument names <see cref="IReadOnlyCollection{String}" />
    /// </summary>
    public IReadOnlyCollection<string> InstrumentNames => _instruments.Values.Select(i => i.Name).ToImmutableList();

    /// <summary>
    ///     Gets the <see cref="IReadOnlyCollection{InstrumentInfo}" />
    /// </summary>
    public IReadOnlyCollection<InstrumentInfo> Instruments => _instruments.Values;

    /// <summary>
    ///     Gets the <see cref="ImmutableDictionary{String, InstrumentInfo}" />
    /// </summary>
    public ImmutableSortedDictionary<string, InstrumentInfo> InstrumentByName =>
        _instruments.ToImmutableSortedDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets the instrument, given its name
    /// </summary>
    /// <param name="name">The instrument name <see cref="string" /></param>
    /// <returns>The <see cref="Nullable{InstrumentInfo}" /></returns>
    public InstrumentInfo? this[string name] => TryGetInstrument(name, out var instrument) ? instrument : null;

    /// <summary>
    ///     Attempts the retrieve an instrument, given its name
    /// </summary>
    /// <param name="name">The instrument name <see cref="string" /></param>
    /// <param name="instrumentInfo">The <see cref="InstrumentInfo" /></param>
    /// <returns>True if instrument is found, false otherwise</returns>
    public bool TryGetInstrument(string name, [MaybeNullWhen(false)] out InstrumentInfo instrumentInfo)
    {
        instrumentInfo = null;
        var entry = _instruments.Values.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            return false; // Failure
        }

        // Success
        instrumentInfo = entry;
        return true;
    }

    /// <summary>
    ///     Populates the instruments from <see cref="InstrumentsConfig" />
    /// </summary>
    private void PopulateInstruments()
    {
        _instruments.Clear();

        // Get all instruments from the F# module
        var instruments = InstrumentsConfig.getAllInstruments();

        foreach (var instrument in instruments)
        {
            // Convert F# tunings list to C# dictionary
            var tuningsDict = instrument.Tunings
                .ToImmutableDictionary(
                    t => t.Name,
                    t => new TuningInfo(t.Name, t, t.Tuning),
                    StringComparer.OrdinalIgnoreCase);

            _instruments.Add(instrument.Name, new(instrument.Name, tuningsDict, null));
        }
    }

    #region Inner Classes

    /// <summary>
    ///     Represents information about a musical instrument.
    /// </summary>
    /// <param name="Name">The name of the instrument.</param>
    /// <param name="Tunings">A dictionary of tunings available for this instrument.</param>
    /// <param name="Icon">Optional SVG icon for the instrument.</param>
    public record InstrumentInfo(string Name, IReadOnlyDictionary<string, TuningInfo> Tunings, string? Icon = null)
    {
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Name} ({Tunings.Count} tunings)";
        }
    }

    /// <summary>
    ///     Represents information about a specific tuning for an instrument.
    /// </summary>
    /// <param name="Name">The name of the tuning.</param>
    /// <param name="TuningInstance">The tuning instance object.</param>
    /// <param name="Tuning">The string representation of the tuning.</param>
    public record TuningInfo(string Name, object TuningInstance, string Tuning)
    {
        /// <summary>
        ///     Indicates if this tuning is the standard tuning for the instrument.
        /// </summary>
        public bool IsStandard =>
            Name.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
            Name.EndsWith(".Standard", StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Name} - {Tuning}";
        }
    }

    #endregion
}
