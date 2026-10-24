namespace GaApi.Tests;

using GaApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

[TestFixture]
public class ChordBsonContractTests
{
    [TestCase("[4, 7]")]
    [TestCase("[{Semitones:4,Function:'Third',IsEssential:true},{Semitones:7,Function:'Fifth',IsEssential:true}]")]
    public void Deserialize_StoredIntervals_PreservesSemitones(string intervals)
    {
        var chord = BsonSerializer.Deserialize<Chord>(BsonDocument.Parse("{Intervals:" + intervals + ",PitchClassSet:[0,4,7]}"));
        Assert.That(chord.Intervals, Is.EqualTo(new[] { 4, 7 }));
        Assert.That(chord.PitchClassSet, Is.EqualTo(new[] { 0, 4, 7 }));
    }

    [TestCase("{}")]
    [TestCase("{Intervals:null}")]
    public void Deserialize_AbsentIntervals_RemainsNull(string document) =>
        Assert.That(BsonSerializer.Deserialize<Chord>(BsonDocument.Parse(document)).Intervals, Is.Null);

    [TestCase("{Intervals:[{Function:'Third'}]}")]
    [TestCase("{Intervals:[{Semitones:'four'}]}")]
    public void Deserialize_InvalidInterval_RejectsInsteadOfInventingPitch(string document) =>
        Assert.Throws<FormatException>(() => BsonSerializer.Deserialize<Chord>(BsonDocument.Parse(document)));

    [Test]
    public void Serialize_KeepsExistingNumericRepresentation()
    {
        var chord = new Chord { Intervals = [4, 7] };
        var document = chord.ToBsonDocument();
        Assert.That(document["Intervals"].AsBsonArray, Is.EqualTo(new BsonArray { 4, 7 }));
        Assert.That(BsonSerializer.Deserialize<Chord>(document).Intervals, Is.EqualTo(chord.Intervals));
    }
}
