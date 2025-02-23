namespace GA.Data.MongoDB.Services.Embeddings;

public class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "text-embedding-ada-002";
}