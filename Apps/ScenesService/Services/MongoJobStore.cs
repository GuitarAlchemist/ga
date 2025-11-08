namespace ScenesService.Services;

using System.Runtime.CompilerServices;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;

public sealed class MongoJobStore : IJobStore
{
    private readonly IMongoCollection<BuildJobDto> _col;

    public MongoJobStore(IMongoDatabase db)
    {
        _col = db.GetCollection<BuildJobDto>("scene_jobs");
        _col.Indexes.CreateOne(new CreateIndexModel<BuildJobDto>(
            Builders<BuildJobDto>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.CreatedUtc)));
    }

    public async Task<string> EnqueueAsync(SceneBuildRequestDto req, CancellationToken ct = default)
    {
        var job = new BuildJobDto(
            Guid.NewGuid().ToString("n"),
            req.SceneId,
            BuildStatus.Queued,
            DateTimeOffset.UtcNow
        );
        await _col.InsertOneAsync(job, cancellationToken: ct);
        // Stocker la "payload" requise pour rebuild :
        await _col.Database.GetCollection<SceneBuildRequestDto>("scene_payloads")
            .InsertOneAsync(req with { }, cancellationToken: ct);
        return job.JobId;
    }

    public async Task<BuildJobDto?> GetAsync(string jobId, CancellationToken ct = default)
    {
        return await _col.Find(x => x.JobId == jobId).FirstOrDefaultAsync(ct);
    }

    public Task UpdateAsync(BuildJobDto job, CancellationToken ct = default)
    {
        return _col.ReplaceOneAsync(x => x.JobId == job.JobId, job, cancellationToken: ct);
    }

    // Stratégie simple: on "réserve" par update atomique Running
    public async IAsyncEnumerable<BuildJobDto> DequeueBatchAsync(int take,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < take; i++)
        {
            var filter = Builders<BuildJobDto>.Filter.Eq(x => x.Status, BuildStatus.Queued);
            var update = Builders<BuildJobDto>.Update
                .Set(x => x.Status, BuildStatus.Running)
                .Set(x => x.StartedUtc, DateTimeOffset.UtcNow);
            var opts = new FindOneAndUpdateOptions<BuildJobDto>
            {
                ReturnDocument = ReturnDocument.After, Sort = Builders<BuildJobDto>.Sort.Ascending(x => x.CreatedUtc)
            };
            var job = await _col.FindOneAndUpdateAsync(filter, update, opts, ct);
            if (job is null)
            {
                yield break;
            }

            yield return job;
        }
    }
}

public sealed class MongoCancelStore : ICancelStore
{
    private readonly IMongoCollection<BsonDocument> _col;

    public MongoCancelStore(IMongoDatabase db)
    {
        _col = db.GetCollection<BsonDocument>("scene_job_cancel");
    }

    public async Task<bool> IsCanceledAsync(string jobId, CancellationToken ct = default)
    {
        return await _col.Find(new BsonDocument("_id", jobId)).AnyAsync(ct);
    }

    public Task CancelAsync(string jobId, CancellationToken ct = default)
    {
        return _col.ReplaceOneAsync(new BsonDocument("_id", jobId), new BsonDocument("_id", jobId),
            new ReplaceOptions { IsUpsert = true }, ct);
    }
}
