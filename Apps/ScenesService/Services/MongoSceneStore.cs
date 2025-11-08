namespace ScenesService.Services;

using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

public sealed class MongoSceneStore : ISceneStore
{
    private readonly GridFSBucket _bucket;
    private readonly IMongoDatabase _db;
    private readonly IMongoCollection<SceneMetaDoc> _meta;

    public MongoSceneStore(string connString, string database = "scenes_db", string bucket = "scenes")
    {
        var client = new MongoClient(connString);
        _db = client.GetDatabase(database);
        _bucket = new GridFSBucket(_db, new GridFSBucketOptions { BucketName = bucket });
        _meta = _db.GetCollection<SceneMetaDoc>("scene_meta");

        // index unique
        _meta.Indexes.CreateOne(
            new CreateIndexModel<SceneMetaDoc>(
                Builders<SceneMetaDoc>.IndexKeys.Ascending(x => x.SceneId),
                new CreateIndexOptions { Unique = true }));
    }

    public async Task SaveAsync(string sceneId, byte[] glb, object meta, string etag, CancellationToken ct = default)
    {
        // Supprimer ancien fichier si existe
        var old = await _meta.Find(x => x.SceneId == sceneId).FirstOrDefaultAsync(ct);
        if (old?.FileId != null)
        {
            try
            {
                await _bucket.DeleteAsync(old.FileId, ct);
            }
            catch
            {
                /* si absent, ok */
            }
        }

        // Upload GLB → GridFS
        var opts = new GridFSUploadOptions
        {
            Metadata = new BsonDocument
            {
                { "sceneId", sceneId },
                { "etag", etag },
                { "contentType", "model/gltf-binary" }
            }
            // chunkSizeBytes: laisser par défaut ou 1–4MB si très gros
        };
        var fileId = await _bucket.UploadFromBytesAsync($"{sceneId}.glb", glb, opts, ct);

        var doc = new SceneMetaDoc
        {
            Id = old?.Id ?? ObjectId.GenerateNewId(),
            SceneId = sceneId,
            FileId = fileId,
            ETag = etag,
            Length = glb.LongLength,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            MetaJson = JsonSerializer.Serialize(meta)
        };

        var up = Builders<SceneMetaDoc>.Update
            .Set(x => x.FileId, fileId)
            .Set(x => x.ETag, etag)
            .Set(x => x.Length, doc.Length)
            .Set(x => x.LastModifiedUtc, doc.LastModifiedUtc)
            .Set(x => x.MetaJson, doc.MetaJson);

        await _meta.UpdateOneAsync(x => x.SceneId == sceneId,
            up, new UpdateOptions { IsUpsert = true }, ct);
    }

    public async Task<(Stream stream, long length, string etag, DateTimeOffset lastModifiedUtc)?> OpenReadAsync(
        string sceneId, CancellationToken ct = default)
    {
        var m = await _meta.Find(x => x.SceneId == sceneId).FirstOrDefaultAsync(ct);
        if (m is null)
        {
            return null;
        }

        var stream = await _bucket.OpenDownloadStreamAsync(m.FileId, cancellationToken: ct);
        // GridFSDownloadStream est seekable (CanSeek=true) → parfait pour Range
        return (stream, m.Length, m.ETag, m.LastModifiedUtc);
    }

    public async Task<(string etag, long length, DateTimeOffset lastModifiedUtc)?> HeadAsync(string sceneId,
        CancellationToken ct = default)
    {
        var m = await _meta.Find(x => x.SceneId == sceneId).FirstOrDefaultAsync(ct);
        return m is null ? null : (m.ETag, m.Length, m.LastModifiedUtc);
    }

    public async Task<string?> MetaJsonAsync(string sceneId, CancellationToken ct = default)
    {
        var m = await _meta.Find(x => x.SceneId == sceneId).FirstOrDefaultAsync(ct);
        return m?.MetaJson;
    }

    public sealed class SceneMetaDoc
    {
        [BsonId] public ObjectId Id { get; set; }
        public string SceneId { get; set; } = default!;
        public ObjectId FileId { get; set; }
        public string ETag { get; set; } = default!;
        public long Length { get; set; }
        public DateTimeOffset LastModifiedUtc { get; set; }
        public string MetaJson { get; set; } = "{}";
    }
}
