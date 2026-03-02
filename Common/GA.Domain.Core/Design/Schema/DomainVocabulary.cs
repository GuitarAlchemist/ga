namespace GA.Domain.Core.Design.Schema;

public record DomainVocabulary(
    List<string> ChordQualities,
    List<string> Extensions,
    List<string> StackingTypes);
