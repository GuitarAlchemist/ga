namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class ScaleDocument : DocumentBase
{
    public required string Name { get; set; }
    public required List<string> Intervals { get; set; }
    public List<string>? Modes { get; set; }
    public required List<string> Notes { get; set; }
    public required string IntervalClassVector { get; set; }
    public bool IsModal { get; set; }
    public string? ModalFamily { get; set; }
    public bool IsNormalForm { get; set; }
    public bool IsClusterFree { get; set; }
    public string? ScaleVideoUrl { get; set; }
    public required string ScalePageUrl { get; set; }
}
