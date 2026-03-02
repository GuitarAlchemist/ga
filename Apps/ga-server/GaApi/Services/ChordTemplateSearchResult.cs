namespace GaApi.Services;

public class ChordTemplateSearchResult
{
    public List<int> PitchClassSet { get; set; } = [];
    public List<TemplateInfo> Templates { get; set; } = [];
    public double Score { get; set; }
}
