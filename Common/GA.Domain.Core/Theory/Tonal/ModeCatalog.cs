namespace GA.Domain.Core.Theory.Tonal;

using System.Collections.Frozen;
using Atonal;
using Business.Config;
using Core.Primitives.Notes;

/// <summary>
///     Catalog of musical modes and scale families.
///     Provides a generic way to access mode definitions and metadata.
/// </summary>
public static class ModeCatalog
{
    private static readonly FrozenDictionary<IntervalClassVector, ModeFamilyMetadata> _metadata;
    private static readonly FrozenDictionary<PitchClassSetId, ModeInfo> _modeLookup;

    static ModeCatalog()
    {
        var metadata = new Dictionary<IntervalClassVector, ModeFamilyMetadata>();
        var modeLookup = new Dictionary<PitchClassSetId, ModeInfo>();
        InitializeFamilies(metadata, modeLookup);
        _metadata = metadata.ToFrozenDictionary();
        _modeLookup = modeLookup.ToFrozenDictionary();
    }

    public static IReadOnlyDictionary<IntervalClassVector, ModeFamilyMetadata> Metadata => _metadata;

    /// <summary>
    ///     Try to get family metadata by Interval Class Vector
    /// </summary>
    public static bool TryGetFamily(IntervalClassVector icv, out ModeFamilyMetadata metadata) =>
        _metadata.TryGetValue(icv, out metadata!);

    /// <summary>
    ///     Try to get mode info by Pitch Class Set ID
    /// </summary>
    public static bool TryGetMode(PitchClassSetId id, out ModeInfo modeInfo) =>
        _modeLookup.TryGetValue(id, out modeInfo!);

    private static void InitializeFamilies(Dictionary<IntervalClassVector, ModeFamilyMetadata> metadataDict,
        Dictionary<PitchClassSetId, ModeInfo> modeLookupDict)
    {
        var families = ModesConfig.GetModalFamilies();
        foreach (var family in families)
        {
            try
            {
                var modeNames = family.Modes.Select(m => m.Name).ToArray();
                var modeCharacteristicIntervals = family.Modes.Select(m => m.CharacteristicIntervals).ToList();
                var firstModeNotes = family.Modes[0].Notes;

                // Parse notes of the first mode to get the parent scale pitch classes
                var noteNames = firstModeNotes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var pitchClasses = noteNames
                    .Select(n => Note.Accidented.Parse(n, null).PitchClass.Value)
                    .ToArray();

                RegisterFamily(family.Name, pitchClasses, modeNames, modeCharacteristicIntervals, metadataDict,
                    modeLookupDict);
            }
            catch (Exception ex)
            {
                // Log error or ignore invalid family data
                Console.WriteLine($"Error registering family {family.Name}: {ex.Message}");
            }
        }
    }

    private static void RegisterFamily(
        string familyName,
        int[] parentScale,
        string[] modeNames,
        IReadOnlyList<IReadOnlyList<string>> modeCharacteristicIntervals,
        Dictionary<IntervalClassVector, ModeFamilyMetadata> metadataDict,
        Dictionary<PitchClassSetId, ModeInfo> modeLookupDict)
    {
        if (modeNames.Length != parentScale.Length)
        {
            throw new ArgumentException(
                $"Mode names count {modeNames.Length} does not match parent scale count {parentScale.Length}");
        }

        List<(string Name, int[] PitchClasses)> modes = [];

        // Sort parent to ensure it's ascending (standard set)
        Array.Sort(parentScale);

        // Calculate intervals of the parent scale
        var count = parentScale.Length;
        var intervals = new int[count];
        for (var i = 0; i < count; i++)
        {
            var current = parentScale[i];
            var next = parentScale[(i + 1) % count];
            if (next < current)
            {
                next += 12; // wrap around
            }

            intervals[i] = next - current;
        }

        // Generate each rotation (Mode on C)
        var rotatedIntervals = new int[count];
        var modePcs = new int[count];

        for (var i = 0; i < count; i++)
        {
            // Rotate intervals by i
            for (var j = 0; j < count; j++)
            {
                rotatedIntervals[j] = intervals[(i + j) % count];
            }

            // Construct pitch classes from intervals starting at 0
            var currentPc = 0;
            for (var k = 0; k < count; k++)
            {
                modePcs[k] = currentPc;
                currentPc = (currentPc + rotatedIntervals[k]) % 12;
            }

            // We need a copy of modePcs for the list and for the PitchClassSet (if it doesn't copy)
            var modePcsCopy = modePcs.ToArray();
            Array.Sort(modePcsCopy); // Ensure sorted for ID creation

            var pcs = new PitchClassSet(modePcsCopy.Select(PitchClass.FromValue));
            modes.Add((modeNames[i], modePcsCopy));

            var characteristicIntervals = modeCharacteristicIntervals.Count > i
                ? modeCharacteristicIntervals[i]
                : Array.Empty<string>();
            modeLookupDict[pcs.Id] = new(familyName, modeNames[i], i + 1, characteristicIntervals);
        }

        // Create metadata
        var definitions = modes.ToArray();

        // Get ICV from the first mode (parent)
        var parentSet = new PitchClassSet(definitions[0].PitchClasses.Select(PitchClass.FromValue));
        var icv = parentSet.IntervalClassVector;

        // Create ModeFamilyMetadata
        var names = definitions.Select(d => d.Name).ToArray();
        var ids = definitions
            .Select(d => new PitchClassSet(d.PitchClasses.Select(PitchClass.FromValue)).Id)
            .ToList();

        var metadata = new ModeFamilyMetadata(familyName, count, names, ids);

        metadataDict.TryAdd(icv, metadata);
    }
}
