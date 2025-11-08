namespace GaMcpServer.Tools;

using GA.Business.Config;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class ModeTool
{
    [McpServerTool]
    [Description("Get all available modes")]
    public static IEnumerable<string> GetAvailableModes()
    {
        return ModesConfig.GetAllModes().Select(m => m.Name);
    }

    [McpServerTool]
    [Description("Get mode information")]
    public static string GetModeInfo(string modeName)
    {
        var modeInfo = ModesConfig.TryGetModeByName(modeName);
        if (modeInfo == null)
        {
            throw new InvalidOperationException($"Mode not found: {modeName}");
        }

        var mode = modeInfo.Value;
        return $"""
                Mode: {mode.Name}
                Intervals: {mode.IntervalClassVector}
                Notes: {mode.Notes}
                Description: {mode.Description}
                Alternate Names: {(mode.AlternateNames != null ? string.Join(", ", mode.AlternateNames.Value) : "None")}
                """;
    }
}
