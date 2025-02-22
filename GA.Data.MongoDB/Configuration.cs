namespace GA.Data.MongoDB;

public class MongoDbConfig
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "GuitarAssistant";
}