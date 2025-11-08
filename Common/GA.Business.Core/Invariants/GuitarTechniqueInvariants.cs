namespace GA.Business.Core.Invariants;

/// <summary>
///     Invariants for <see cref="GuitarTechniqueDefinition" />.
/// </summary>
public static class GuitarTechniqueInvariants
{
    public static IReadOnlyCollection<IInvariant<GuitarTechniqueDefinition>> GetAll()
    {
        return new IInvariant<GuitarTechniqueDefinition>[]
        {
            new TechniqueNameInvariant(),
            new DescriptionLengthInvariant(),
            new CategoryInvariant(),
            new ExampleCompletenessInvariant()
        }.ToImmutableArray();
    }

    private sealed class TechniqueNameInvariant : InvariantBase<GuitarTechniqueDefinition>
    {
        public override string InvariantName => "TechniqueNameNotEmpty";
        public override string Description => "Technique name must be specified.";

        public override InvariantValidationResult Validate(GuitarTechniqueDefinition technique)
        {
            return string.IsNullOrWhiteSpace(technique.Name)
                ? Failure("Technique name cannot be empty.", nameof(GuitarTechniqueDefinition.Name))
                : Success();
        }
    }

    private sealed class DescriptionLengthInvariant : InvariantBase<GuitarTechniqueDefinition>
    {
        public override string InvariantName => "TechniqueDescriptionLength";
        public override string Description => "Description should provide at least 20 characters of context.";
        public override InvariantSeverity Severity => InvariantSeverity.Warning;

        public override InvariantValidationResult Validate(GuitarTechniqueDefinition technique)
        {
            return string.IsNullOrWhiteSpace(technique.Description) || technique.Description.Length < 20
                ? Failure("Technique description should be at least 20 characters.",
                    nameof(GuitarTechniqueDefinition.Description),
                    technique.Description ?? string.Empty)
                : Success();
        }
    }

    private sealed class CategoryInvariant : InvariantBase<GuitarTechniqueDefinition>
    {
        private static readonly HashSet<string> Allowed =
        [
            "legato", "picking", "rhythm", "harmony", "improvisation", "theory"
        ];

        public override string InvariantName => "TechniqueCategoryValid";
        public override string Description => "Category should match known groupings.";
        public override InvariantSeverity Severity => InvariantSeverity.Warning;

        public override InvariantValidationResult Validate(GuitarTechniqueDefinition technique)
        {
            if (string.IsNullOrWhiteSpace(technique.Category))
            {
                return Success();
            }

            return Allowed.Contains(technique.Category.Trim().ToLowerInvariant())
                ? Success()
                : Failure($"Technique category '{technique.Category}' is not recognized.",
                    nameof(GuitarTechniqueDefinition.Category),
                    technique.Category);
        }
    }

    private sealed class ExampleCompletenessInvariant : InvariantBase<GuitarTechniqueDefinition>
    {
        public override string InvariantName => "TechniqueExamplesConsistent";
        public override string Description => "Examples should provide both chord and scale context.";
        public override InvariantSeverity Severity => InvariantSeverity.Info;

        public override InvariantValidationResult Validate(GuitarTechniqueDefinition technique)
        {
            if (technique.Examples == null || technique.Examples.Count == 0)
            {
                return Success();
            }

            var incomplete = technique.Examples.FirstOrDefault(example =>
                string.IsNullOrWhiteSpace(example.Scale) ||
                example.Chords == null || example.Chords.Count == 0);

            return incomplete is null
                ? Success()
                : Failure("Technique examples should specify at least one chord and the associated scale.",
                    nameof(GuitarTechniqueDefinition.Examples));
        }
    }
}
