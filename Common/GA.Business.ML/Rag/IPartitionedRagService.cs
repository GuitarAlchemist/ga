namespace GA.Business.ML.Rag;

using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Service that performs semantic search across partitioned knowledge bases.
/// Supports filtering by knowledge type and weighting results.
/// </summary>
public interface IPartitionedRagService
{
    /// <summary>
    /// Searches for relevant knowledge across the specified partitions.
    /// </summary>
    /// <param name="query">The semantic query.</param>
    /// <param name="partitions">Which knowledge partitions to search.</param>
    /// <param name="topK">Maximum results per partition or total.</param>
    /// <returns>Ranked discovery of musical knowledge.</returns>
    Task<PartitionedRagResponse> QueryAsync(
        string query, 
        KnowledgeType[] partitions, 
        int topK = 5);
}
