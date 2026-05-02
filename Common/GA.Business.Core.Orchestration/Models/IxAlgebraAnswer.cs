namespace GA.Business.Core.Orchestration.Models;

public sealed record IxAlgebraAnswer(
    string NaturalLanguageAnswer,
    string QueryType,
    IReadOnlyDictionary<string, string> Facts,
    GroundingMetadata Grounding);
