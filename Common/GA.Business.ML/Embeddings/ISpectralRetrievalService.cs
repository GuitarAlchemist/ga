namespace GA.Business.ML.Embeddings;

using static SpectralRetrievalService;

/// <summary>
///     Abstraction for spectral retrieval operations using the OPTIC-K schema.
///     Enables testability and swappable implementations.
/// </summary>
public interface ISpectralRetrievalService
{
    /// <summary>
    ///     Performs a weighted similarity search across the vector partitions.
    /// </summary>
    /// <param name="queryEmbedding">The OPTIC-K embedding to search with.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <param name="preset">Search preset (Tonal, Atonal, Jazz, etc.).</param>
    /// <param name="quality">Optional chord quality filter.</param>
    /// <param name="extension">Optional extension filter.</param>
    /// <param name="stackingType">Optional stacking type filter.</param>
    /// <param name="noteCount">Optional note count filter.</param>
    /// <returns>Ranked results with similarity scores.</returns>
    IEnumerable<(ChordVoicingRagDocument Doc, double Score)> Search(
        float[] queryEmbedding,
        int topK = 10,
        SearchPreset preset = SearchPreset.Tonal,
        string? quality = null,
        string? extension = null,
        string? stackingType = null,
        int? noteCount = null);

    /// <summary>
    ///     Finds neighbors based purely on Spectral Geometry (harmonic smoothness/voice-leading).
    /// </summary>
    /// <param name="queryEmbedding">The OPTIC-K embedding to search with.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <returns>Ranked results by spectral similarity.</returns>
    IEnumerable<(ChordVoicingRagDocument Doc, double Score)> FindSpectralNeighbors(
        float[] queryEmbedding,
        int topK = 10);
}
