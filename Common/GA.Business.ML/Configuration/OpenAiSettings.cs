namespace GA.Business.ML.Configuration;

public class OpenAiSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string ModelName { get; init; } = "text-embedding-ada-002";
}
