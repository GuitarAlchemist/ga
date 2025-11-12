namespace GA.Business.Core.Invariants;

/// <summary>
///     Invariants for <see cref="ChordProgressionDefinition" />.
/// </summary>
public static class ChordProgressionInvariants
{
    public static IReadOnlyCollection<IInvariant<ChordProgressionDefinition>> GetAll()
    {
        return new IInvariant<ChordProgressionDefinition>[]
        {
            new ProgressionNameInvariant(),
            new RomanNumeralStructureInvariant(),
            new ChordCountMatchesRomanNumeralsInvariant(),
            new DifficultyInvariant()
        }.ToImmutableArray();
    }

    private sealed class ProgressionNameInvariant : InvariantBase<ChordProgressionDefinition>
    {
        public override string InvariantName => "ProgressionNameNotEmpty";
        public override string Description => "Progression name must be provided.";

        public override InvariantValidationResult Validate(ChordProgressionDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.Name)
                ? Failure("Chord progression name cannot be empty.", nameof(ChordProgressionDefinition.Name))
                : Success();
        }
    }

    private sealed class RomanNumeralStructureInvariant : InvariantBase<ChordProgressionDefinition>
    {
        private static readonly Regex _pattern = new("^[ivxlcdmIVXLCDM]+(?:sus|maj|add|dim|aug|m)?[0-9]*$",
            RegexOptions.Compiled);

        public override string InvariantName => "RomanNumeralStructure";
        public override string Description => "Roman numerals must use standard chord symbols.";

        public override InvariantValidationResult Validate(ChordProgressionDefinition definition)
        {
            if (definition.RomanNumerals == null || definition.RomanNumerals.Count == 0)
            {
                return Failure("At least one roman numeral is required.",
                    nameof(ChordProgressionDefinition.RomanNumerals));
            }

            foreach (var numeral in definition.RomanNumerals)
            {
                if (string.IsNullOrWhiteSpace(numeral) || !_pattern.IsMatch(numeral))
                {
                    return Failure($"Invalid roman numeral '{numeral}'.",
                        nameof(ChordProgressionDefinition.RomanNumerals),
                        numeral ?? string.Empty);
                }
            }

            return Success();
        }
    }

    private sealed class ChordCountMatchesRomanNumeralsInvariant : InvariantBase<ChordProgressionDefinition>
    {
        public override string InvariantName => "ChordCountMatchesRomanNumerals";
        public override string Description => "Chord list should match the number of roman numerals.";

        public override InvariantValidationResult Validate(ChordProgressionDefinition definition)
        {
            if (definition.Chords == null || definition.Chords.Count == 0)
            {
                return Success(); // allow omitted chord list
            }

            if (definition.RomanNumerals == null || definition.RomanNumerals.Count == 0)
            {
                return Failure("Chords provided without roman numerals context.",
                    nameof(ChordProgressionDefinition.Chords),
                    definition.Chords.Count);
            }

            return definition.Chords.Count == definition.RomanNumerals.Count
                ? Success()
                : Failure("Chord count must match the number of roman numerals.",
                    nameof(ChordProgressionDefinition.Chords),
                    definition.Chords.Count);
        }
    }

    private sealed class DifficultyInvariant : InvariantBase<ChordProgressionDefinition>
    {
        private static readonly HashSet<string> _allowed =
        [
            "beginner", "intermediate", "advanced", "expert"
        ];

        public override string InvariantName => "ProgressionDifficultyValid";
        public override string Description => "Difficulty must be a recognized descriptor.";
        public override InvariantSeverity Severity => InvariantSeverity.Warning;

        public override InvariantValidationResult Validate(ChordProgressionDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.Difficulty))
            {
                return Success();
            }

            return _allowed.Contains(definition.Difficulty.Trim().ToLowerInvariant())
                ? Success()
                : Failure($"Difficulty '{definition.Difficulty}' is not recognized.",
                    nameof(ChordProgressionDefinition.Difficulty),
                    definition.Difficulty);
        }
    }
}
