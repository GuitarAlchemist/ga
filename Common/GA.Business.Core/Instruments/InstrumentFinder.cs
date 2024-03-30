namespace GA.Business.Core.Instruments;

public class InstrumentFinder
{
    private readonly Dictionary<string, Dictionary<string, string>> _instrumentsWithTunings = [];
    private readonly Config.Instruments.Config _config;

    public InstrumentFinder()
    {
        _config = new Config.Instruments.Config();

        PopulateInstruments();
        _config.Changed += (sender, e) => PopulateInstruments();
    }

    private void PopulateInstruments()
    {
        _instrumentsWithTunings.Clear();

        var instrumentsType = typeof(Config.Instruments.Config);

        foreach (var instrumentProp in instrumentsType.GetProperties())
        {
            var instrumentName = instrumentProp.Name;
            var tunings = new Dictionary<string, string>();
            var instrumentValue = instrumentProp.GetValue(_config, null);
            if (instrumentValue == null) continue;

            foreach (var tuningProp in instrumentValue.GetType().GetProperties())
            {
                var tuningName = tuningProp.Name;
                var tuningDisplayName = tuningName;
                var tuningValue = tuningProp.GetValue(instrumentValue, null)?.ToString();
                if (tuningValue == null) continue;
                tunings.Add(tuningDisplayName, tuningValue);
            }

            _instrumentsWithTunings.Add(instrumentName, tunings);
        }
    }

    public IEnumerable<string> ListAllInstruments() => _instrumentsWithTunings.Keys;
    public Dictionary<string, string>? GetInstrumentTunings(string instrumentFullName) => _instrumentsWithTunings.GetValueOrDefault(instrumentFullName);

}