namespace GaMcpServer.Tools;

using GA.Business.Config;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class InstrumentTool
{
    [McpServerTool]
    [Description("Get string notes for a specific instrument and tuning")]
    public static string GetTuning(string instrument, string tuningName)
    {
        var instrumentConfig = InstrumentsConfig.tryGetInstrument(instrument).Value;
        if (instrumentConfig == null)
        {
            throw new InvalidOperationException($"Instrument not found: {instrument}");
        }

        var tuning = instrumentConfig.Tunings.FirstOrDefault(t => t.Name == tuningName);
        if (tuning == null)
        {
            throw new InvalidOperationException($"Tuning not found: {tuningName}");
        }

        return tuning.Tuning;
    }

    [McpServerTool]
    [Description("Get all available tunings for an instrument")]
    public static IEnumerable<string> GetAvailableTunings(string instrument)
    {
        var instrumentConfig = InstrumentsConfig.tryGetInstrument(instrument).Value;
        if (instrumentConfig == null)
        {
            throw new InvalidOperationException($"Instrument not found: {instrument}");
        }

        var tunings = instrumentConfig.Tunings;
        return tunings.Select(t => t.Name);
    }

    [McpServerTool]
    [Description("Get all available instruments")]
    public static IEnumerable<string> GetAvailableInstruments()
    {
        return InstrumentsConfig.listAllInstrumentNames();
    }

    [McpServerTool]
    [Description("Reload instruments configuration")]
    public static bool ReloadConfig()
    {
        return InstrumentsConfig.reloadConfig().IsOk;
    }
}
