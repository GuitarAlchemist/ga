namespace GaApi.Services;

public class CacheStatistics
{
    public long RegularHits { get; set; }
    public long RegularMisses { get; set; }
    public double RegularHitRate { get; set; }
    public long SemanticHits { get; set; }
    public long SemanticMisses { get; set; }
    public double SemanticHitRate { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }

    public double TotalHitRate => TotalHits + TotalMisses > 0
        ? (double)TotalHits / (TotalHits + TotalMisses)
        : 0;
}
