namespace GA.Data.MongoDB.Services;

using Models.Rag;

public interface IRagSyncService<T> : ISyncService<T> where T : RagDocumentBase
{
}