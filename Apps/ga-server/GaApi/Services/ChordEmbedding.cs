namespace GaApi.Services;

/// <summary>
///     Chord with embedding data
/// </summary>
public record ChordEmbedding(
    int Id,
    string Name,
    string Quality,
    string Extension,
    string StackingType,
    int NoteCount,
    string Description,
    double[] Embedding);
