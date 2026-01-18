namespace GA.AI.Service.Services;

public interface IOllamaService
{
    Task<string> GenerateAsync(string prompt, string? model = null);
    Task<string> AnalyzeBenchmarkAsync(string benchmarkName, object benchmarkData);
    Task<string> ExplainVoicingAsync(string voicingName, object voicingData);
}
