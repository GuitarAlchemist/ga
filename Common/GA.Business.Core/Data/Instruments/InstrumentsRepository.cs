namespace GA.Business.Core.Data.Instruments;

[PublicAPI]
public sealed class InstrumentsRepository
{
    #region Inner Classes

    /// <summary>
    /// Represents information about a musical instrument.
    /// </summary>
    /// <param name="Name">The name of the instrument.</param>
    /// <param name="Tunings">A dictionary of tunings available for this instrument.</param>
    public record InstrumentInfo(string Name, IReadOnlyDictionary<string, TuningInfo> Tunings)
    {
        /// <inheritdoc />
        public override string ToString() => $"{Name} ({Tunings.Count} tunings)";
    }

    /// <summary>
    /// Represents information about a specific tuning for an instrument.
    /// </summary>
    /// <param name="Name">The name of the tuning.</param>
    /// <param name="TuningInstance">The tuning instance object.</param>
    /// <param name="Tuning">The string representation of the tuning.</param>
    public record TuningInfo(string Name, object TuningInstance, string Tuning)
    {
        /// <inheritdoc />
        public override string ToString() => $"{Name} - {Tuning}";
    }

    #endregion

    public static readonly InstrumentsRepository Instance = new();

    private readonly SortedDictionary<string, InstrumentInfo> _instruments = [];
    private readonly Config.Instruments.Config _config;

    public InstrumentsRepository()
    {
        _config = new();
        PopulateInstruments();
        _config.Changed += (_, _) => PopulateInstruments();
    }

    /// <summary>
    /// Gets the instrument names <see cref="IReadOnlyCollection{String}"/>
    /// </summary>
    public IReadOnlyCollection<string> InstrumentNames => _instruments.Values.Select(i => i.Name).ToImmutableList();

    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{InstrumentInfo}"/>
    /// </summary>
    public IReadOnlyCollection<InstrumentInfo> Instruments => _instruments.Values;

    /// <summary>
    ///  Gets the <see cref="ImmutableDictionary{String, InstrumentInfo}"/>
    /// </summary>
    public ImmutableSortedDictionary<string, InstrumentInfo> InstrumentByName =>
        _instruments.ToImmutableSortedDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the instrument, given its name
    /// </summary>
    /// <param name="name">The instrument name <see cref="string"/></param>
    /// <returns>The <see cref="Nullable{InstrumentInfo}"/></returns>
    public InstrumentInfo? this[string name] => TryGetInstrument(name, out var instrument) ? instrument : null;

    /// <summary>
    /// Attempts the retrieve an instrument, given its name
    /// </summary>
    /// <param name="name">The instrument name <see cref="string"/></param>
    /// <param name="instrumentInfo">The <see cref="InstrumentInfo"/></param>
    /// <returns>True if instrument is found, false otherwise</returns>
    public bool TryGetInstrument(string name, [MaybeNullWhen(false)] out InstrumentInfo instrumentInfo)
    {
        instrumentInfo = null;
        var entry = _instruments.Values.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return false; // Failure

        // Success
        instrumentInfo = entry;
        return true;
    }

    /// <summary>
    /// Populates the instrument from <see cref="Config.Instruments.Config"/>
    /// </summary>
    private void PopulateInstruments()
    {
        _instruments.Clear();
        var configType = typeof(Config.Instruments.Config);

        foreach (var instrumentProp in configType.GetProperties())
        {
            var instrumentName = instrumentProp.Name;
            var instrumentValue = instrumentProp.GetValue(_config);
            if (instrumentValue == null) continue;

            var displayNameProp = instrumentValue.GetType().GetProperty("DisplayName");
            var displayName = displayNameProp != null ? (string)displayNameProp.GetValue(instrumentValue)! : instrumentName;
            _instruments.Add(displayName, new(displayName, CreateTunings(instrumentValue)));
        }
        return;
    }

    private static ImmutableDictionary<string, TuningInfo> CreateTunings(object instrumentValue)
    {
        var tuningsBuilder = ImmutableDictionary.CreateBuilder<string, TuningInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var tuningProp in instrumentValue.GetType().GetProperties().Where(p => p.Name != "DisplayName"))
        {
            var tuningInstance = tuningProp.GetValue(instrumentValue);
            if (tuningInstance == null) continue;

            // Attempt to get the DisplayName property from the tuning instance
            var tuningType = tuningInstance.GetType();
            var tuningDisplayNameProp = tuningType.GetProperty("DisplayName");
            var tuningTuningProp = tuningType.GetProperty("Tuning");
            if (tuningTuningProp == null) continue;
            if (tuningTuningProp.GetValue(tuningInstance) is not string tuning) continue;

            // If the DisplayName property exists, use it; otherwise, use the property name
            var tuningDisplayName = GetTuningDisplayName(tuningDisplayNameProp, tuningInstance, tuningProp);

            tuningsBuilder.Add(tuningDisplayName, new(tuningDisplayName, tuningInstance, tuning));
        }
        return tuningsBuilder.ToImmutable();

        static string GetTuningDisplayName(PropertyInfo? tuningDisplayNameProp, object tuningInstance, MemberInfo tuningProp)
        {
            string result;
            if (tuningDisplayNameProp != null
                &&
                tuningDisplayNameProp.GetValue(tuningInstance) is string displayName
                &&
                !string.IsNullOrWhiteSpace(displayName))
            {
                result = displayName;
            }
            else
            {
                result = tuningProp.Name;
            }

            return result;
        }
    }
}