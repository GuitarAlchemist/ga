namespace GA.Business.Core.Invariants;

/// <summary>
///     Invariants for <see cref="SpecializedTuningDefinition" />.
/// </summary>
public static class SpecializedTuningInvariants
{
    public static IReadOnlyCollection<IInvariant<SpecializedTuningDefinition>> GetAll()
    {
        return new IInvariant<SpecializedTuningDefinition>[]
        {
            new TuningNameInvariant(),
            new PitchClassRangeInvariant(),
            new StringConfigurationInvariant()
        }.ToImmutableArray();
    }

    private sealed class TuningNameInvariant : InvariantBase<SpecializedTuningDefinition>
    {
        public override string InvariantName => "TuningNameNotEmpty";
        public override string Description => "Specialized tuning must have a name.";

        public override InvariantValidationResult Validate(SpecializedTuningDefinition tuning)
        {
            return string.IsNullOrWhiteSpace(tuning.Name)
                ? Failure("Tuning name cannot be empty.", nameof(SpecializedTuningDefinition.Name))
                : Success();
        }
    }

    private sealed class PitchClassRangeInvariant : InvariantBase<SpecializedTuningDefinition>
    {
        public override string InvariantName => "TuningPitchClassesValid";
        public override string Description => "Pitch classes must lie between 0 and 11.";

        public override InvariantValidationResult Validate(SpecializedTuningDefinition tuning)
        {
            if (tuning.PitchClasses == null || tuning.PitchClasses.Count == 0)
            {
                return Failure("Pitch classes must contain at least one value.",
                    nameof(SpecializedTuningDefinition.PitchClasses));
            }

            var invalid = tuning.PitchClasses.FirstOrDefault(pc => pc is < 0 or > 11);
            return invalid is < 0 or > 11
                ? Failure($"Pitch class '{invalid}' is outside the valid range 0-11.",
                    nameof(SpecializedTuningDefinition.PitchClasses),
                    invalid)
                : Success();
        }
    }

    private sealed class StringConfigurationInvariant : InvariantBase<SpecializedTuningDefinition>
    {
        public override string InvariantName => "TuningStringConfigurationValid";
        public override string Description => "String configuration metadata should be specified.";
        public override InvariantSeverity Severity => InvariantSeverity.Warning;

        public override InvariantValidationResult Validate(SpecializedTuningDefinition tuning)
        {
            if (tuning.Configuration == null || tuning.Configuration.Count == 0)
            {
                return Failure("Configuration metadata should include at least one key/value pair.",
                    nameof(SpecializedTuningDefinition.Configuration));
            }

            if (!tuning.Configuration.TryGetValue("Strings", out var stringCount) ||
                string.IsNullOrWhiteSpace(stringCount))
            {
                return Failure("Configuration should specify the number of strings.",
                    nameof(SpecializedTuningDefinition.Configuration));
            }

            return Success();
        }
    }
}
