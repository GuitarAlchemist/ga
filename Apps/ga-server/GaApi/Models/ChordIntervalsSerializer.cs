namespace GaApi.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

/// <summary>Reads exported interval documents while retaining the API's numeric interval contract.</summary>
public sealed class ChordIntervalsSerializer : SerializerBase<int[]?>
{
    private static readonly ArraySerializer<int> Items = new(new IntervalSerializer());

    public override int[]? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
        Items.Deserialize(context, args);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, int[]? value) =>
        Items.Serialize(context, args, value!);

    private sealed class IntervalSerializer : SerializerBase<int>
    {
        public override int Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.GetCurrentBsonType() != BsonType.Document)
                return Int32Serializer.Instance.Deserialize(context, args);

            // GaDataCLI exports Semitones, Function, and IsEssential per interval.
            // Missing or invalid semitones must fail rather than invent a pitch.
            var document = BsonDocumentSerializer.Instance.Deserialize(context);
            return document["Semitones"].AsInt32;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, int value) =>
            Int32Serializer.Instance.Serialize(context, args, value);
    }
}
