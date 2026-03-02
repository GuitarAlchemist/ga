namespace GaApi.Services;

/// <summary>
///     Search filters for hybrid search
/// </summary>
public record ChordSearchFilters(
    string? Quality = null,
    string? Extension = null,
    string? StackingType = null,
    int? NoteCount = null);
