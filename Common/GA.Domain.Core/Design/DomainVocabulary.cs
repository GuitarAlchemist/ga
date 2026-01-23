namespace GA.Domain.Core.Design;

public record DomainVocabulary(
    List<string> ChordQualities,
    List<string> Extensions,
    List<string> StackingTypes);