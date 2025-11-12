namespace GA.DocumentProcessing.Service.Models;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "GuitarAlchemist";
    public string CollectionName { get; set; } = "ProcessedDocuments";
}

