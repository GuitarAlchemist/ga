namespace GaApi.Controllers;

public record HybridSearchRequest(
    string Query,
    string? Quality = null,
    string? Extension = null,
    string? StackingType = null,
    int? NoteCount = null,
    int Limit = 10,
    int NumCandidates = 100,
    double[]? Vector = null
);