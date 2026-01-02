namespace GA.Business.Core.Unified;

using Atonal;
using System.Collections.Concurrent;

/// <summary>
///     Default implementation of <see cref="IUnifiedModeService"/> built on existing atonal primitives.
/// </summary>
public sealed class UnifiedModeService : IUnifiedModeService
{
    private static readonly ConcurrentDictionary<(UnifiedModeId Id, int RotationIndex), UnifiedModeDescription> _describeCache = new();

    public UnifiedModeInstance FromScaleMode(Tonal.Modes.ScaleMode mode, PitchClass root)
    {
        // 1) Extract pitch classes from the mode notes
        // 2) Transpose so the provided root becomes 0
        // 3) Build a PitchClassSet and delegate to FromPitchClassSet

        var notePitchClasses = mode.Notes
            .Select(n => n.PitchClass)
            .Distinct()
            .ToList();

        if (notePitchClasses.Count == 0)
        {
            // Degenerate safeguard: fall back to a trivial set with just the root
            return FromPitchClassSet(new PitchClassSet(new[] { root }), root);
        }

        var modeRootPc = notePitchClasses[0];
        var delta = (root.Value - modeRootPc.Value + 12) % 12;

        var transposed = notePitchClasses
            .Select(pc => PitchClass.FromValue((pc.Value + delta) % 12))
            .ToList();

        var set = new PitchClassSet(transposed);
        return FromPitchClassSet(set, root);
    }

    public UnifiedModeInstance FromPitchClassSet(PitchClassSet set, PitchClass root)
    {
        // Build set-class identity (prime form + ICV)
        var setClass = new SetClass(set);
        var icv = setClass.IntervalClassVector;
        var prime = setClass.PrimeForm;

        // Find modal family if any
        ModalFamily? family = ModalFamily.TryGetValue(icv, out var fam) ? fam : null;

        var isSymmetric = IsSymmetricSet(prime);
        var @class = new UnifiedModeClass(icv, prime, isSymmetric, family);

        // Determine rotation index within the family if possible
        var rotationIndex = 0;
        PitchClassSet rotationSet = set;

        if (family != null)
        {
            // Family members are transpositions that contain 0. Prefer keeping the provided set as the rotation
            // to preserve the caller's intended ordering; compute an index only if an exact member matches.
            var index = family.Modes.FindIndex(m => m.Id.Equals(set.Id));
            rotationIndex = index >= 0 ? index : 0;
            // Keep rotationSet as the provided set to avoid switching to the family’s prime representative
        }

        return new UnifiedModeInstance(@class, rotationIndex, root, rotationSet);
    }

    public IEnumerable<UnifiedModeInstance> EnumerateRotations(UnifiedModeClass modeClass, PitchClass root)
    {
        if (modeClass.Family is { } family)
        {
            for (var i = 0; i < family.Modes.Count; i++)
            {
                var member = family.Modes[i];
                yield return new UnifiedModeInstance(modeClass, i, root, member);
            }
            yield break;
        }

        // Non-modal: yield the class itself as a single rotation
        yield return new UnifiedModeInstance(modeClass, 0, root, modeClass.PrimeForm);
    }

    public UnifiedModeDescription Describe(UnifiedModeInstance instance)
    {
        // Cache by unified class id and rotation index (root is not part of textual description currently)
        var cacheKey = (instance.Class.Id, instance.RotationIndex);
        if (_describeCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var cls = instance.Class;
        var icvText = cls.IntervalClassVector.ToString();

        var familyInfo = cls.Family == null
            ? null
            : $"Family: {cls.Family.NoteCount} notes, {cls.Family.Modes.Count} modes";

        var symmetry = cls.IsSymmetric ? "symmetric" : null;

        var primaryName = cls.Family == null
            ? $"SetClass {cls.IntervalClassVector.Id}"
            : $"Modal family {cls.IntervalClassVector.Id} (mode {instance.RotationIndex + 1})";

        var summary = $"Card:{cls.Cardinality} | ICV:{icvText} | Rot:{instance.RotationIndex}";

        // Prime form textual representation
        var primeFormText = "[" + string.Join(",", cls.PrimeForm.Select(pc => pc.Value).OrderBy(v => v)) + "]";

        // Forte number: try canonical mapping first, then fall back to internal stable label
        string? forte = TryGetCanonicalForte(cls.PrimeForm) ?? TryComputeForteLikeNumber(cls.PrimeForm);

        var description = new UnifiedModeDescription
        {
            PrimaryName = primaryName,
            Summary = summary,
            IntervalClassVector = icvText,
            Cardinality = cls.Cardinality,
            RotationIndex = instance.RotationIndex,
            Symmetry = symmetry,
            FamilyInfo = familyInfo,
            PrimeForm = primeFormText,
            ForteNumber = forte
        };

        _describeCache[cacheKey] = description;
        return description;
    }

    private static bool IsSymmetricSet(PitchClassSet set)
    {
        // A set is rotationally symmetric if there exists k (1..11) such that Transpose(k) == set
        var id = set.Id;
        for (var k = 1; k < 12; k++)
        {
            if (id.Transpose(k).Equals(id))
            {
                return true;
            }
        }
        return false;
    }

    private static string? TryGetCanonicalForte(PitchClassSet primeForm)
    {
        if (ForteCatalog.TryGetForteNumber(primeForm, out var forte))
        {
            return forte.ToString();
        }
        return null;
    }

    private static string? TryComputeForteLikeNumber(PitchClassSet primeForm)
    {
        try
        {
            // Map the set's SetClass into an index within all set classes with the same cardinality.
            var setClass = new SetClass(primeForm);
            var cardinality = setClass.Cardinality;

            var sameCardinality = SetClass.Items
                .Where(sc => sc.Cardinality.Equals(cardinality))
                .OrderBy(sc => sc.IntervalClassVector.Id)
                .ThenBy(sc => sc.PrimeForm.Id.Value)
                .ToList();

            var index = sameCardinality.FindIndex(sc => sc.PrimeForm.Id.Equals(primeForm.Id));
            if (index < 0)
            {
                return null;
            }

            var forte = new ForteNumber(cardinality, index + 1);
            return forte.ToString();
        }
        catch
        {
            return null;
        }
    }
}
