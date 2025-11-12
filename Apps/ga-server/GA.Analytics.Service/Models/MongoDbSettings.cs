namespace GA.Analytics.Service.Models;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "guitar-alchemist";
    public CollectionSettings Collections { get; set; } = new();
}

public class CollectionSettings
{
    public string Chords { get; set; } = "chords";
    public string ChordTemplates { get; set; } = "chord-templates";
    public string Scales { get; set; } = "scales";
    public string Progressions { get; set; } = "progressions";
}
