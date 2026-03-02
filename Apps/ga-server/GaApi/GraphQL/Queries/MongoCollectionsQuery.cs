namespace GaApi.GraphQL.Queries;

using GaApi.Models;
using GaApi.Services;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Types;
using MongoDB.Driver;

[ExtendObjectType("Query")]
public class MongoCollectionsQuery
{
    [UsePaging(IncludeTotalCount = true, DefaultPageSize = 20)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Chord> GetChords([Service] MongoDbService mongoService) => mongoService.Chords.AsQueryable();

    [UsePaging(IncludeTotalCount = true, DefaultPageSize = 20)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<VoicingEntity> GetVoicings([Service] MongoDbService mongoService) => mongoService.Voicings.AsQueryable();
}
