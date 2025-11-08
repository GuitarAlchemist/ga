namespace GA.Business.Core.Invariants;

/// <summary>
///     Provides invariant definitions for <see cref="IconicChordDefinition" />.
/// </summary>
public static class IconicChordInvariants
{
    /// <summary>
    ///     Returns all invariants that must hold for iconic chords.
    /// </summary>
    public static IReadOnlyCollection<IInvariant<IconicChordDefinition>> GetAll()
    {
        return new IInvariant<IconicChordDefinition>[]
        {
            new NameNotEmptyInvariant(),
            new TheoreticalNameValidInvariant(),
            new PitchClassesValidInvariant(),
            new GuitarVoicingValidInvariant(),
            new GenreValidInvariant(),
            new AlternateNamesUniqueInvariant()
        }.ToImmutableArray();
    }

    private sealed class NameNotEmptyInvariant : InvariantBase<IconicChordDefinition>
    {
        public override string InvariantName => "NameNotEmpty";
        public override string Description => "Chord name cannot be blank.";

        public override InvariantValidationResult Validate(IconicChordDefinition chord)
        {
            return string.IsNullOrWhiteSpace(chord.Name)
                ? Failure("Chord name cannot be empty.", nameof(IconicChordDefinition.Name), chord.Name ?? string.Empty)
                : Success();
        }
    }

    private sealed class TheoreticalNameValidInvariant : InvariantBase<IconicChordDefinition>
    {
        private static readonly Regex Pattern = new("^[A-G](?:#|b)?[0-9A-Za-z+#\\-]*$", RegexOptions.Compiled);

        public override string InvariantName => "TheoreticalNameValid";
        public override string Description => "Theoretical name must start with a valid pitch class.";

        public override InvariantValidationResult Validate(IconicChordDefinition chord)
        {
            if (string.IsNullOrWhiteSpace(chord.TheoreticalName))
            {
                return Failure("Theoretical name is required.", nameof(IconicChordDefinition.TheoreticalName),
                    chord.TheoreticalName ?? string.Empty);
            }

            return Pattern.IsMatch(chord.TheoreticalName)
                ? Success()
                : Failure("Theoretical name must start with a valid root note (A-G with optional #/b).",
                    nameof(IconicChordDefinition.TheoreticalName),
                    chord.TheoreticalName);
        }
    }

    private sealed class PitchClassesValidInvariant : InvariantBase<IconicChordDefinition>
    {
        public override string InvariantName => "PitchClassesValid";
        public override string Description => "Pitch classes must be between 0-11 and contain at least one value.";

        public override InvariantValidationResult Validate(IconicChordDefinition chord)
        {
            if (chord.PitchClasses == null || chord.PitchClasses.Count == 0)
            {
                return Failure("Pitch classes cannot be null or empty.", nameof(IconicChordDefinition.PitchClasses));
            }

            var invalid = chord.PitchClasses.FirstOrDefault(pc => pc is < 0 or > 11);
            return invalid is < 0 or > 11
                ? Failure($"Pitch class '{invalid}' is outside the valid range 0-11.",
                    nameof(IconicChordDefinition.PitchClasses), invalid)
                : Success();
        }
    }

    private sealed class GuitarVoicingValidInvariant : InvariantBase<IconicChordDefinition>
    {
        public override string InvariantName => "GuitarVoicingValid";
        public override string Description => "Guitar voicing must describe a playable chord.";

        public override InvariantValidationResult Validate(IconicChordDefinition chord)
        {
            if (chord.GuitarVoicing == null || chord.GuitarVoicing.Count == 0)
            {
                return Failure("Guitar voicing must contain fret positions.",
                    nameof(IconicChordDefinition.GuitarVoicing));
            }

            var playedStrings = chord.GuitarVoicing.Count(fret => fret >= 0);

            if (playedStrings < 2)
            {
                return Failure("At least 2 strings must be played for a valid voicing.",
                    nameof(IconicChordDefinition.GuitarVoicing),
                    playedStrings);
            }

            if (chord.GuitarVoicing.Any(fret => fret is > 24))
            {
                return Failure("Fret positions must be within 0-24.",
                    nameof(IconicChordDefinition.GuitarVoicing));
            }

            return Success();
        }
    }

    private sealed class GenreValidInvariant : InvariantBase<IconicChordDefinition>
    {
        private static readonly HashSet<string> KnownGenres =
        [
            "rock", "jazz", "blues", "metal", "folk", "country", "pop", "fusion", "classical", "latin"
        ];

        public override string InvariantName => "GenreValid";
        public override string Description => "Genre should match a known value.";
        public override InvariantSeverity Severity => InvariantSeverity.Warning;

        public override InvariantValidationResult Validate(IconicChordDefinition chord)
        {
            if (string.IsNullOrWhiteSpace(chord.Genre))
            {
                return Success(); // allow unspecified genre
            }

            return KnownGenres.Contains(chord.Genre.Trim().ToLowerInvariant())
                ? Success()
                : Failure($"Genre '{chord.Genre}' is not in the recognized list.",
                    nameof(IconicChordDefinition.Genre),
                    chord.Genre);
        }
    }

    private sealed class AlternateNamesUniqueInvariant : InvariantBase<IconicChordDefinition>
    {
        public override string InvariantName => "AlternateNamesUnique";
        public override string Description => "Alternate names must be unique and different from the main name.";

        public override InvariantValidationResult Validate(IconicChordDefinition chord)
        {
            if (chord.AlternateNames == null || chord.AlternateNames.Count == 0)
            {
                return Success();
            }

            var normalized = chord.AlternateNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim().ToLowerInvariant())
                .ToList();

            if (normalized.Any(name => name == chord.Name.Trim().ToLowerInvariant()))
            {
                return Failure("Alternate names should not duplicate the main chord name.",
                    nameof(IconicChordDefinition.AlternateNames));
            }

            if (normalized.Count != normalized.Distinct().Count())
            {
                return Failure("Duplicate alternate names detected (case-insensitive compare).",
                    nameof(IconicChordDefinition.AlternateNames));
            }

            return Success();
        }
    }
}
