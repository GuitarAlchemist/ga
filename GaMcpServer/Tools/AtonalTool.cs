using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Primitives;
using ModelContextProtocol.Server;

namespace GaMcpServer.Tools;

[McpServerToolType]
public static class AtonalTool
{
    [McpServerTool, Description("Get all set classes")]
    public static IEnumerable<string> GetSetClasses() => SetClass.Items.Select(sc => sc.ToString());

    [McpServerTool, Description("Get all modal set classes")]
    public static IEnumerable<string> GetModalSetClasses() => SetClass.ModalItems.Select(sc => sc.ToString());

    [McpServerTool, Description("Get modal family information")]
    public static string GetModalFamilyInfo(string intervalVector)
    {
        if (!IntervalClassVector.TryParse(intervalVector, null, out var parsedIntervalClassVector)) 
            throw new InvalidOperationException($"Invalid interval vector: {intervalVector}");
        if (!ModalFamily.TryGetValue(parsedIntervalClassVector, out var family)) 
            throw new InvalidOperationException($"Modal family not found for interval vector: {intervalVector}");
            
        return $"""
            Note Count: {family.NoteCount}
            Interval Vector: {family.IntervalClassVector}
            Mode Count: {family.Modes.Count}
            Prime Mode: {family.PrimeMode}
            """;
    }

    [McpServerTool, Description("Get all cardinalities")]
    public static IEnumerable<string> GetCardinalities() => Cardinality.Items.Select(c => c.ToString());
}