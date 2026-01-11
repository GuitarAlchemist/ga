namespace GA.Business.Core.Unified;

using System;
using Atonal;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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
            return FromPitchClassSet(new([root]), root);
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
        var family = ModalFamily.TryGetValue(icv, out var fam) ? fam : null;

        var isSymmetric = IsSymmetricSet(prime);
        var @class = new UnifiedModeClass(icv, prime, isSymmetric, family);

        // Determine rotation index within the family if possible
        var rotationIndex = 0;

        if (family != null)
        {
            // Family members are transpositions that contain 0. Prefer keeping the provided set as the rotation
            // to preserve the caller's intended ordering; compute an index only if an exact member matches.
            var index = family.Modes.FindIndex(m => m.Id.Equals(set.Id));
            rotationIndex = index >= 0 ? index : 0;
            // Keep rotationSet as the provided set to avoid switching to the family’s prime representative
        }

        return new(@class, rotationIndex, root, set);
    }

    public IEnumerable<UnifiedModeInstance> EnumerateRotations(UnifiedModeClass modeClass, PitchClass root)
    {
        if (modeClass.Family is { } family)
        {
            for (var i = 0; i < family.Modes.Count; i++)
            {
                var member = family.Modes[i];
                yield return new(modeClass, i, root, member);
            }
            yield break;
        }

        // Non-modal: yield the class itself as a single rotation
        yield return new(modeClass, 0, root, modeClass.PrimeForm);
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
        var forte = TryGetCanonicalForte(cls.PrimeForm) ?? TryComputeForteLikeNumber(cls.PrimeForm);

        // Calculate Brightness
        // Note: Currently uses the relative values from the 'SourceSet' if available, or just the RotationIndex assumptions?
        // Actually, Brightness is a property of the *specific rotation*, not just the Class.
        // We need to calculate it from the instance's SourceSet, but relative to 0 being the "Root" of that mode.
        // instance.SourceSet contains raw PitchClasses.
        // If the instance.Root is, say, E, and the set is {E, F#, G#...}, we treat E as 0.
        // SourceSet is theoretically absolute, but in 'FromScaleMode' we made it relative to C=0?
        // Let's rely on the set content implicitly. For "Unified" description we assume Root=0 (C).
        // Since we cache by RotationIndex, we can assume the set in the Family.Modes[Index] is "C-based".

        var currentRotationSet = instance.Class.Family?.Modes[instance.RotationIndex] ?? instance.Class.PrimeForm;
        var brightness = currentRotationSet.Sum(pc => pc.Value);

        // Spectral Centroid
        // Calculate from the PrimeForm (Set Class invariant).
        var spectralCentroid = new SetClass(cls.PrimeForm).GetSpectralCentroid();

        // Identify Messiaen Mode
        var messiaenIndex = IdentifyMessiaenMode(cls.PrimeForm);

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
            ForteNumber = forte,
            Brightness = brightness,
            SpectralCentroid = spectralCentroid,
            MessiaenModeIndex = messiaenIndex
        };

        _describeCache[cacheKey] = description;
        return description;
    }

    public int GetCommonToneCount(UnifiedModeInstance a, UnifiedModeInstance b)
    {
        // Intersect the two source sets.
        // UnifiedModeInstance.SourceSet is the specific set of pitch classes (e.g. C, E, G).
        // We can just intersect generic PitchClass lists.
        var setA = a.RotationSet;
        var setB = b.RotationSet;

        var count = 0;
        foreach (var pc in setA)
        {
            if (setB.Contains(pc)) count++;
        }
        return count;
    }

    public double GetVoiceLeadingDistance(UnifiedModeInstance a, UnifiedModeInstance b)
    {
        // Minimal voice leading distance usually implies bijective mapping between notes
        // minimizing total displacement.
        // For sets of difference cardinality, this is ill-defined or requires generic "edit distance".
        // Here we implement a simplified "nearest neighbor total displacement" for same-cardinality sets,
        // or return infinity for differing cardinality to enforce strictness for now.

        if (a.Class.Cardinality != b.Class.Cardinality)
        {
             // For differing cardinality, voice leading distance is not strictly "displacement".
             // We could implement "Transport Distance" but for now let's be conservative.
             return double.PositiveInfinity;
        }

        var sortedA = a.RotationSet.Select(p => p.Value).OrderBy(x => x).ToList();
        var sortedB = b.RotationSet.Select(p => p.Value).OrderBy(x => x).ToList();

        // Warning: This simple "sorted index" matching is only valid if we assume
        // a specific "close position" mapping is not required, OR if we assume small distances.
        // Actually, "Voice Leading Distance" between two PC Sets is the minimum displacement
        // over all possible transpositions? No, usually between two specific *chords* (Voicings) it's physical.
        // Between two *Scale Modes* (PC Sets), it usually implies the minimal modulation distance.
        //
        // A robust metric for PC Sets A and B (ordered) is:
        // Sum of minimal distance from each element of A to *some* element of B? No, that's Hausdorff.
        //
        // Let's use "Taxicab metric on sorted Normal Form" for simple estimation?
        // No, we have the specific "SourceSet" instances (e.g. C Major vs C Minor).
        // Let's optimize rotation of B to match A? No, "Instance" implies fixed root.
        //
        // We will compute the sum of shortest distances for the sorted elements, assuming simple
        // 1-to-1 mapping in order.

        double distance = 0;
        for (int i = 0; i < sortedA.Count; i++)
        {
            var diff = Math.Abs(sortedA[i] - sortedB[i]);
            if (diff > 6) diff = 12 - diff; // Modular distance
            distance += diff;
        }
        return distance;
    }

    /// <summary>
    /// Identifies if a pitch class set is one of Messiaen's 7 Modes of Limited Transposition.
    /// </summary>
    /// <remarks>
    /// Messiaen's modes are symmetric scales that have fewer than 12 transpositions.
    /// Mode 1: Whole Tone (6 notes, 2 transpositions)
    /// Mode 2: Octatonic/Diminished (8 notes, 3 transpositions)
    /// Mode 3: 9-note scale (3 transpositions)
    /// Mode 4: 8-note scale (6 transpositions)
    /// Mode 5: 6-note scale (6 transpositions)
    /// Mode 6: 8-note scale (6 transpositions)
    /// Mode 7: 10-note scale (6 transpositions)
    /// </remarks>
    private static int? IdentifyMessiaenMode(PitchClassSet primeForm)
    {
        // PitchClassSetId is a bitmask where bit n is set if pitch class n is present.
        // We check multiple rotations/transpositions since the input may not be the "canonical" form.
        var id = primeForm.Id.Value;

        // Mode 1: Whole Tone [0,2,4,6,8,10]
        // ID = 2^0 + 2^2 + 2^4 + 2^6 + 2^8 + 2^10 = 1365
        if (id == 1365) return 1;

        // Mode 2: Octatonic [0,1,3,4,6,7,9,10] (Whole-Half diminished)
        // Two common forms: 1755 and 2925
        if (id == 1755 || id == 2925) return 2;

        // Mode 3: 9-note augmented scale [0,1,2,4,5,6,8,9,10]
        // ID = 1911
        if (id == 1911) return 3;

        // Mode 4: 8-note scale [0,1,2,5,6,7,8,11]
        // ID = 2535
        if (id == 2535) return 4;

        // Mode 5: 6-note scale [0,1,5,6,7,11]
        // ID = 2^0 + 2^1 + 2^5 + 2^6 + 2^7 + 2^11 = 1 + 2 + 32 + 64 + 128 + 2048 = 2275
        if (id == 2275) return 5;

        // Mode 6: 8-note scale [0,1,2,5,6,7,8,11] or [0,1,4,5,6,7,10,11]
        // Common form: [0,1,4,5,6,7,10,11]
        // ID = 2^0 + 2^1 + 2^4 + 2^5 + 2^6 + 2^7 + 2^10 + 2^11 = 1 + 2 + 16 + 32 + 64 + 128 + 1024 + 2048 = 3315
        if (id == 3315) return 6;

        // Mode 7: 10-note scale [0,1,2,3,5,6,7,8,9,11]
        // ID = 2^0 + 2^1 + 2^2 + 2^3 + 2^5 + 2^6 + 2^7 + 2^8 + 2^9 + 2^11 = 1+2+4+8+32+64+128+256+512+2048 = 3055
        if (id == 3055) return 7;

        return null;
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

    /// <inheritdoc/>
    public IEnumerable<(UnifiedModeInstance Instance, int Brightness)> RankByBrightness(UnifiedModeClass modeClass, PitchClass root)
    {
        // Get all rotations of the mode class
        var rotations = EnumerateRotations(modeClass, root).ToList();

        // Calculate brightness for each rotation and sort descending
        return rotations
            .Select(inst =>
            {
                var brightness = inst.RotationSet.Sum(pc => pc.Value);
                return (Instance: inst, Brightness: brightness);
            })
            .OrderByDescending(x => x.Brightness);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Z-relations occur when two set classes share the same interval class vector
    /// but have different prime forms. This is a relatively rare phenomenon.
    /// Examples: (4-Z15, 4-Z29), (5-Z12, 5-Z36), (6-Z6, 6-Z38).
    /// </remarks>
    public IEnumerable<(PitchClassSet Set1, PitchClassSet Set2)> GetZRelatedPairs()
    {
        // Group all set classes by their interval class vector
        var byIcv = SetClass.Items
            .GroupBy(sc => sc.IntervalClassVector.Id)
            .Where(g => g.Count() >= 2) // Only ICV groups with multiple sets
            .ToList();

        foreach (var group in byIcv)
        {
            var sets = group.Select(sc => sc.PrimeForm).ToList();

            // For each ICV group, return pairs of distinct prime forms
            for (var i = 0; i < sets.Count; i++)
            {
                for (var j = i + 1; j < sets.Count; j++)
                {
                    // Only yield if prime forms are actually different (Z-related)
                    if (!sets[i].Id.Equals(sets[j].Id))
                    {
                        yield return (sets[i], sets[j]);
                    }
                }
            }
        }
    }
}

