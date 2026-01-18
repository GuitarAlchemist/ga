using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

var settings = Options.Create(new MongoDbSettings 
{ 
    ConnectionString = "mongodb://localhost:27017", 
    DatabaseName = "guitaralchemist" 
});

var mongo = new MongoDbService(settings);
Console.WriteLine("Dropping 'voicings' collection...");
await mongo.Database.DropCollectionAsync("voicings");
Console.WriteLine("Dropped.");
