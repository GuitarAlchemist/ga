namespace ChatbotExample1.Services.Ingestion;

public interface IIngestionSource
{
    string SourceId { get; }

    Task<ImmutableList<IngestedDocument>>
        GetNewOrModifiedDocumentsAsync(IQueryable<IngestedDocument> existingDocuments);

    Task<ImmutableList<IngestedDocument>> GetDeletedDocumentsAsync(IQueryable<IngestedDocument> existingDocuments);

    Task<ImmutableList<SemanticSearchRecord>> CreateRecordsForDocumentAsync(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, string documentId);
}
