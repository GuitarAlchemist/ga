namespace GaApi.Services;

using System.Collections.Immutable;
using System.Globalization;
using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Scales;
using Models;
using ChordTemplate = GA.Business.Core.Chords.ChordTemplate;
using Position = GA.Business.Core.Fretboard.Primitives.Position;

/// <summary>
///     Provides hierarchical music data (Set Classes → Forte Numbers → Prime Forms → Chords → Voicings → Scales)
///     backed entirely by programmatic sources in GA.Business.Core.
/// </summary>
public class MusicHierarchyService
{
    private const int MaxSetClasses = 256;
    private const int MaxPrimeForms = 256;
    private const int MaxScales = 200;
    private const int MaxChordTemplatesPerPrime = 6;
    private const int MaxChordVoicingsTotal = 400;
    private const int MaxVoicingsPerPrime = 12;
    private const int MaxScalesPerPrime = 8;
    private readonly IReadOnlyList<MusicHierarchyItem> _chordItems;
    private readonly IReadOnlyDictionary<string, MusicHierarchyItem> _chordLookup;
    private readonly IReadOnlyDictionary<string, string> _chordPrimeLookup;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<MusicHierarchyItem>> _chordsByPrimeId;
    private readonly IReadOnlyList<MusicHierarchyItem> _forteItems;
    private readonly IReadOnlyDictionary<string, string> _forteToPrimeId;
    private readonly IReadOnlyList<MusicHierarchyLevelInfo> _levels;
    private readonly IReadOnlyList<MusicHierarchyItem> _primeItems;
    private readonly IReadOnlyDictionary<string, PitchClassSet> _primePitchSets;
    private readonly IReadOnlyDictionary<string, string> _primeToSetClassId;
    private readonly IReadOnlyList<MusicHierarchyItem> _scaleItems;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<MusicHierarchyItem>> _scalesByPrimeId;

    private readonly IReadOnlyList<MusicHierarchyItem> _setClassItems;
    private readonly IReadOnlyDictionary<string, SetClass> _setClassLookup;
    private readonly IReadOnlyDictionary<string, string> _setClassToPrimeId;
    private readonly IReadOnlyList<MusicHierarchyItem> _voicingItems;
    private readonly IReadOnlyDictionary<string, string> _voicingPrimeLookup;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<MusicHierarchyItem>> _voicingsByPrimeId;

    public MusicHierarchyService()
    {
        _setClassItems = BuildSetClassItems(out _setClassLookup, out _setClassToPrimeId);
        _forteItems = BuildForteItems(_setClassLookup, _setClassToPrimeId, out _forteToPrimeId);
        _primeItems = BuildPrimeItems(_setClassLookup, out _primePitchSets, out _primeToSetClassId);
        _chordItems = BuildChordItems(
            _primePitchSets,
            out _chordLookup,
            out _chordPrimeLookup,
            out _chordsByPrimeId);
        _voicingItems = BuildVoicingItems(
            _primePitchSets,
            out _voicingsByPrimeId,
            out _voicingPrimeLookup);
        _scaleItems = BuildScaleItems(_primePitchSets, out _scalesByPrimeId);
        _levels = BuildLevelInfo(
            _setClassItems.Count,
            _forteItems.Count,
            _primeItems.Count,
            _chordItems.Count,
            _voicingItems.Count,
            _scaleItems.Count);
    }

    public IReadOnlyList<MusicHierarchyLevelInfo> GetLevels()
    {
        return _levels;
    }

    public IReadOnlyList<MusicHierarchyItem> GetItems(
        MusicHierarchyLevel level,
        string? parentId = null,
        int take = 200,
        string? search = null)
    {
        var source = level switch
        {
            MusicHierarchyLevel.SetClass => _setClassItems,
            MusicHierarchyLevel.ForteNumber => _forteItems,
            MusicHierarchyLevel.PrimeForm => _primeItems,
            MusicHierarchyLevel.Chord => _chordItems,
            MusicHierarchyLevel.ChordVoicing => ResolveVoicings(parentId),
            MusicHierarchyLevel.Scale => _scaleItems,
            _ => Array.Empty<MusicHierarchyItem>()
        };

        source = ApplyParentFilter(level, parentId, source);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            source = source.Where(item =>
                item.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (item.Description?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.Tags.Any(tag => tag.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        if (take > 0)
        {
            source = source.Take(take);
        }

        return source.ToList();
    }

    private IEnumerable<MusicHierarchyItem> ApplyParentFilter(
        MusicHierarchyLevel level,
        string? parentId,
        IEnumerable<MusicHierarchyItem> source)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return source;
        }

        return level switch
        {
            MusicHierarchyLevel.ForteNumber => FilterByMetadata(source, "ParentSetClassId", parentId),
            MusicHierarchyLevel.PrimeForm => FilterPrimeForms(parentId, source),
            MusicHierarchyLevel.Chord => GetChordsForParent(parentId, source),
            MusicHierarchyLevel.Scale => GetScalesForParent(parentId, source),
            _ => source
        };
    }

    private static IEnumerable<MusicHierarchyItem> FilterByMetadata(
        IEnumerable<MusicHierarchyItem> source,
        string key,
        string expected)
    {
        return source.Where(item =>
            item.Metadata.TryGetValue(key, out var value) &&
            string.Equals(value, expected, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<MusicHierarchyItem> FilterPrimeForms(string parentId, IEnumerable<MusicHierarchyItem> source)
    {
        var primeId = TryResolvePrimeId(parentId);
        if (primeId == null)
        {
            return source;
        }

        return source.Where(item => string.Equals(item.Id, primeId, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<MusicHierarchyItem> GetChordsForParent(string parentId,
        IEnumerable<MusicHierarchyItem> fallback)
    {
        var primeId = TryResolvePrimeId(parentId);
        if (primeId != null && _chordsByPrimeId.TryGetValue(primeId, out var chords))
        {
            return chords;
        }

        return fallback;
    }

    private IEnumerable<MusicHierarchyItem> GetScalesForParent(string parentId,
        IEnumerable<MusicHierarchyItem> fallback)
    {
        var primeId = TryResolvePrimeId(parentId);
        if (primeId != null && _scalesByPrimeId.TryGetValue(primeId, out var scales))
        {
            return scales;
        }

        return fallback;
    }

    private IEnumerable<MusicHierarchyItem> ResolveVoicings(string? parentId)
    {
        var primeId = TryResolvePrimeId(parentId);
        if (primeId != null && _voicingsByPrimeId.TryGetValue(primeId, out var voicings))
        {
            return voicings;
        }

        return _voicingItems;
    }

    private string? TryResolvePrimeId(string? parentId)
    {
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return null;
        }

        if (parentId.StartsWith("PRIME::", StringComparison.OrdinalIgnoreCase))
        {
            return parentId;
        }

        if (parentId.StartsWith("SET_CLASS::", StringComparison.OrdinalIgnoreCase) &&
            _setClassToPrimeId.TryGetValue(parentId, out var primeFromSet))
        {
            return primeFromSet;
        }

        if (parentId.StartsWith("FORTE::", StringComparison.OrdinalIgnoreCase) &&
            _forteToPrimeId.TryGetValue(parentId, out var primeFromForte))
        {
            return primeFromForte;
        }

        if (parentId.StartsWith("CHORD::", StringComparison.OrdinalIgnoreCase) &&
            _chordPrimeLookup.TryGetValue(parentId, out var primeFromChord))
        {
            return primeFromChord;
        }

        if (parentId.StartsWith("VOICING::", StringComparison.OrdinalIgnoreCase) &&
            _voicingPrimeLookup.TryGetValue(parentId, out var primeFromVoicing))
        {
            return primeFromVoicing;
        }

        return null;
    }

    private IReadOnlyList<MusicHierarchyItem> BuildSetClassItems(
        out IReadOnlyDictionary<string, SetClass> lookup,
        out IReadOnlyDictionary<string, string> setClassToPrime)
    {
        var items = new List<MusicHierarchyItem>();
        var dict = new Dictionary<string, SetClass>();
        var map = new Dictionary<string, string>();

        foreach (var setClass in SetClass.Items.Take(MaxSetClasses))
        {
            var prime = setClass.PrimeForm;
            if (prime is null)
            {
                continue;
            }

            var id = $"SET_CLASS::{prime.Id.Value}";
            var primeId = $"PRIME::{prime.Id.Value}";

            var metadata = new Dictionary<string, string>
            {
                ["PrimeFormId"] = primeId,
                ["Cardinality"] = setClass.Cardinality.Value.ToString(CultureInfo.InvariantCulture),
                ["IntervalVector"] = setClass.IntervalClassVector.ToString()
            };

            items.Add(new MusicHierarchyItem
            {
                Id = id,
                Name = $"Set Class {setClass.Cardinality.Value}-{setClass.IntervalClassVector.Id}",
                Level = MusicHierarchyLevel.SetClass,
                Category = setClass.IsModal ? "Modal" : "Atonal",
                Description = prime.Name,
                Tags = prime.Select(pc => pc.ToString()).ToList(),
                Metadata = metadata
            });

            dict[id] = setClass;
            map[id] = primeId;
        }

        lookup = dict;
        setClassToPrime = map;
        return items;
    }

    private IReadOnlyList<MusicHierarchyItem> BuildForteItems(
        IReadOnlyDictionary<string, SetClass> setClassLookup,
        IReadOnlyDictionary<string, string> setClassToPrime,
        out IReadOnlyDictionary<string, string> forteToPrime)
    {
        var items = new List<MusicHierarchyItem>();
        var map = new Dictionary<string, string>();

        foreach (var (setId, setClass) in setClassLookup)
        {
            if (!setClassToPrime.TryGetValue(setId, out var primeId))
            {
                continue;
            }

            var forteNumber = ComputeForteNumber(setClass);
            var id = $"FORTE::{setClass.Cardinality.Value}-{setClass.IntervalClassVector.Id.Value}";

            items.Add(new MusicHierarchyItem
            {
                Id = id,
                Name = $"Forte {forteNumber}",
                Level = MusicHierarchyLevel.ForteNumber,
                Category = $"Cardinality {setClass.Cardinality.Value}",
                Description = $"Derived from {setId}",
                Tags = new[] { forteNumber },
                Metadata = new Dictionary<string, string>
                {
                    ["ForteNumber"] = forteNumber,
                    ["Cardinality"] = setClass.Cardinality.Value.ToString(CultureInfo.InvariantCulture),
                    ["ParentSetClassId"] = setId,
                    ["ParentPrimeId"] = primeId
                }
            });

            map[id] = primeId;
        }

        forteToPrime = map;
        return items;
    }

    private IReadOnlyList<MusicHierarchyItem> BuildPrimeItems(
        IReadOnlyDictionary<string, SetClass> setClassLookup,
        out IReadOnlyDictionary<string, PitchClassSet> primePitchSets,
        out IReadOnlyDictionary<string, string> primeToSetClass)
    {
        var items = new List<MusicHierarchyItem>();
        var pitchSets = new Dictionary<string, PitchClassSet>();
        var map = new Dictionary<string, string>();

        foreach (var (setId, setClass) in setClassLookup.Take(MaxPrimeForms))
        {
            var prime = setClass.PrimeForm;
            if (prime is null)
            {
                continue;
            }

            var primeId = $"PRIME::{prime.Id.Value}";

            items.Add(new MusicHierarchyItem
            {
                Id = primeId,
                Name = prime.Name,
                Level = MusicHierarchyLevel.PrimeForm,
                Category = $"Cardinality {setClass.Cardinality.Value}",
                Description = $"Prime form for {setId}",
                Tags = prime.Select(pc => pc.ToString()).ToList(),
                Metadata = new Dictionary<string, string>
                {
                    ["ParentSetClassId"] = setId,
                    ["Cardinality"] = setClass.Cardinality.Value.ToString(CultureInfo.InvariantCulture)
                }
            });

            pitchSets[primeId] = prime;
            map[primeId] = setId;
        }

        primePitchSets = pitchSets;
        primeToSetClass = map;
        return items;
    }

    private IReadOnlyList<MusicHierarchyItem> BuildChordItems(
        IReadOnlyDictionary<string, PitchClassSet> primePitchSets,
        out IReadOnlyDictionary<string, MusicHierarchyItem> chordLookup,
        out IReadOnlyDictionary<string, string> chordPrimeLookup,
        out IReadOnlyDictionary<string, IReadOnlyList<MusicHierarchyItem>> chordsByPrime)
    {
        var grouped = new Dictionary<string, List<MusicHierarchyItem>>();
        var lookup = new Dictionary<string, MusicHierarchyItem>();
        var primeMap = new Dictionary<string, string>();
        var allChords = new List<MusicHierarchyItem>();

        var templates = ChordTemplateRegistry.GetAllTemplates();
        var byPrime = templates
            .Select(template => new
            {
                Template = template,
                PrimeId = template.PitchClassSet.PrimeForm?.Id.Value
            })
            .Where(entry => entry.PrimeId.HasValue)
            .GroupBy(entry => $"PRIME::{entry.PrimeId!.Value}");

        foreach (var group in byPrime)
        {
            if (!primePitchSets.ContainsKey(group.Key))
            {
                continue;
            }

            var ordered = group
                .Select(entry => entry.Template)
                .OrderBy(t => t.NoteCount)
                .ThenBy(t => t.Extension)
                .ThenBy(t => t.StackingType)
                .Take(MaxChordTemplatesPerPrime)
                .ToList();

            var primeChordList = new List<MusicHierarchyItem>();

            for (var i = 0; i < ordered.Count; i++)
            {
                var template = ordered[i];
                var chordId = $"CHORD::{group.Key}:{i}";
                var description = DescribeChordTemplate(template);
                var metadata = new Dictionary<string, string>
                {
                    ["ParentPrimeId"] = group.Key,
                    ["NoteCount"] = template.NoteCount.ToString(CultureInfo.InvariantCulture),
                    ["Quality"] = template.Quality.ToString(),
                    ["Extension"] = template.Extension.ToString(),
                    ["Stacking"] = template.StackingType.ToString(),
                    ["PitchClasses"] = string.Join(" ", template.PitchClassSet.Select(pc => pc.ToString()))
                };

                if (template is ChordTemplate.TonalModal tonal)
                {
                    metadata["ParentScale"] = tonal.ParentScale.Name;
                    metadata["ScaleDegree"] = tonal.ScaleDegree.ToString(CultureInfo.InvariantCulture);
                    metadata["HarmonicFunction"] = tonal.HarmonicFunction;
                }
                else if (template is ChordTemplate.Analytical analytical)
                {
                    metadata["AnalysisMethod"] = analytical.AnalysisMethod;
                }

                var item = new MusicHierarchyItem
                {
                    Id = chordId,
                    Name = template.Name,
                    Level = MusicHierarchyLevel.Chord,
                    Category = template.Quality.ToString(),
                    Description = description,
                    Tags = template.Intervals.Select(i => i.Function.ToString()).ToList(),
                    Metadata = metadata
                };

                primeChordList.Add(item);
                lookup[chordId] = item;
                primeMap[chordId] = group.Key;
                allChords.Add(item);
            }

            if (primeChordList.Count > 0)
            {
                grouped[group.Key] = primeChordList;
            }
        }

        chordLookup = lookup;
        chordPrimeLookup = primeMap;
        chordsByPrime = grouped.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<MusicHierarchyItem>)kvp.Value);

        return allChords;
    }

    private IReadOnlyList<MusicHierarchyItem> BuildVoicingItems(
        IReadOnlyDictionary<string, PitchClassSet> primePitchSets,
        out IReadOnlyDictionary<string, IReadOnlyList<MusicHierarchyItem>> groupedByPrime,
        out IReadOnlyDictionary<string, string> voicingPrimeLookup)
    {
        var fretboard = Fretboard.Default;
        var analyses = FretboardChordAnalyzer
            .GenerateAllFiveFretSpanChords(fretboard, maxFret: 12)
            .Where(a => a.ChordTemplate != null)
            .ToList();

        var grouped = new Dictionary<string, List<MusicHierarchyItem>>();
        var primeLookup = new Dictionary<string, string>();
        var flattened = new List<MusicHierarchyItem>();

        foreach (var analysis in analyses)
        {
            var prime = analysis.PitchClassSet.PrimeForm;
            if (prime is null)
            {
                continue;
            }

            var primeId = $"PRIME::{prime.Id.Value}";
            if (!primePitchSets.ContainsKey(primeId))
            {
                continue;
            }

            var list = grouped.TryGetValue(primeId, out var existing)
                ? existing
                : grouped[primeId] = new List<MusicHierarchyItem>();

            if (list.Count >= MaxVoicingsPerPrime || flattened.Count >= MaxChordVoicingsTotal)
            {
                continue;
            }

            var id = $"VOICING::{primeId}:{list.Count}";
            var (frets, strings) = FormatPositions(analysis.Positions);
            var metadata = new Dictionary<string, string>
            {
                ["ParentPrimeId"] = primeId,
                ["ChordName"] = analysis.ChordName,
                ["Root"] = analysis.Root.ToString(),
                ["Difficulty"] = analysis.Difficulty.ToString(),
                ["FretSpan"] = analysis.FretSpan.ToString(CultureInfo.InvariantCulture),
                ["Frets"] = frets,
                ["Strings"] = strings,
                ["LowestFret"] = analysis.LowestFret.ToString(CultureInfo.InvariantCulture),
                ["HighestFret"] = analysis.HighestFret.ToString(CultureInfo.InvariantCulture),
                ["IsPlayable"] = analysis.IsPlayable.ToString()
            };

            if (!string.IsNullOrWhiteSpace(analysis.IconicName))
            {
                metadata["IconicName"] = analysis.IconicName;
            }

            if (!string.IsNullOrWhiteSpace(analysis.IconicDescription))
            {
                metadata["IconicDescription"] = analysis.IconicDescription;
            }

            var item = new MusicHierarchyItem
            {
                Id = id,
                Name = analysis.ChordName,
                Level = MusicHierarchyLevel.ChordVoicing,
                Category = analysis.Difficulty.ToString(),
                Description = analysis.VoicingDescription,
                Tags = analysis.Notes.Select(n => n.ToString()).ToList(),
                Metadata = metadata
            };

            list.Add(item);
            flattened.Add(item);
            primeLookup[id] = primeId;

            if (flattened.Count >= MaxChordVoicingsTotal)
            {
                break;
            }
        }

        groupedByPrime = grouped.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<MusicHierarchyItem>)kvp.Value);
        voicingPrimeLookup = primeLookup;
        return flattened;
    }

    private IReadOnlyList<MusicHierarchyItem> BuildScaleItems(
        IReadOnlyDictionary<string, PitchClassSet> primePitchSets,
        out IReadOnlyDictionary<string, IReadOnlyList<MusicHierarchyItem>> scalesByPrime)
    {
        var items = new List<MusicHierarchyItem>();
        var scaleSets = new List<(MusicHierarchyItem Item, PitchClassSet Set)>();
        var grouped = new Dictionary<string, List<MusicHierarchyItem>>();
        var index = 0;

        foreach (var scale in Scale.Items.Take(MaxScales))
        {
            var notes = scale.Select(note => note.ToString()).ToList();
            if (notes.Count == 0)
            {
                continue;
            }

            var modalFamily = scale.ModalFamily?.ToString() ?? (scale.IsModal ? "Modal" : "Custom");
            var id = $"SCALE::{index++}";
            var item = new MusicHierarchyItem
            {
                Id = id,
                Name = $"{notes[0]} {modalFamily}",
                Level = MusicHierarchyLevel.Scale,
                Category = modalFamily,
                Description = string.Join(" ", notes),
                Tags = notes,
                Metadata = new Dictionary<string, string>
                {
                    ["Root"] = notes[0],
                    ["NoteCount"] = notes.Count.ToString(CultureInfo.InvariantCulture),
                    ["ModalFamily"] = modalFamily,
                    ["PrimeFormId"] = $"PRIME::{scale.PitchClassSet.PrimeForm?.Id.Value ?? -1}"
                }
            };

            items.Add(item);
            scaleSets.Add((item, scale.PitchClassSet));
        }

        foreach (var (primeId, chordSet) in primePitchSets)
        {
            var matches = scaleSets
                .Where(tuple => chordSet.IsSubsetOf(tuple.Set))
                .Take(MaxScalesPerPrime)
                .Select(tuple => tuple.Item)
                .ToList();

            if (matches.Count > 0)
            {
                grouped[primeId] = matches;
            }
        }

        scalesByPrime = grouped.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<MusicHierarchyItem>)kvp.Value);

        return items;
    }

    private static IReadOnlyList<MusicHierarchyLevelInfo> BuildLevelInfo(
        int setClassCount,
        int forteCount,
        int primeCount,
        int chordCount,
        int voicingCount,
        int scaleCount)
    {
        return
        [
            new()
            {
                Level = MusicHierarchyLevel.SetClass,
                DisplayName = "Set Classes",
                Description = "Atonal equivalence classes grouped by cardinality and interval vectors.",
                TotalItems = setClassCount,
                PrimaryMetric = "Cardinality",
                Highlights = ["Prime forms", "Interval vectors", "Modal tagging"]
            },
            new()
            {
                Level = MusicHierarchyLevel.ForteNumber,
                DisplayName = "Forte Numbers",
                Description = "Programmatic Forte identifiers aligned with each set class.",
                TotalItems = forteCount,
                PrimaryMetric = "Cardinality",
                Highlights = ["TI-equivalence", "Cardinality buckets"]
            },
            new()
            {
                Level = MusicHierarchyLevel.PrimeForm,
                DisplayName = "Prime Forms",
                Description = "Canonical pitch-class sets exposed via GA.Business.Core.",
                TotalItems = primeCount,
                PrimaryMetric = "Pitch content",
                Highlights = ["Pitch-class IDs", "Modal detection"]
            },
            new()
            {
                Level = MusicHierarchyLevel.Chord,
                DisplayName = "Chords",
                Description = "Chord templates generated entirely from procedural factories.",
                TotalItems = chordCount,
                PrimaryMetric = "Chord family",
                Highlights = ["Triads", "Sevenths", "Extensions"]
            },
            new()
            {
                Level = MusicHierarchyLevel.ChordVoicing,
                DisplayName = "Voicings",
                Description = "Real fretboard voicings synthesized from the FretboardChordAnalyzer.",
                TotalItems = voicingCount,
                PrimaryMetric = "Difficulty",
                Highlights = ["Playable spans", "Iconic shapes", "Invariant IDs"]
            },
            new()
            {
                Level = MusicHierarchyLevel.Scale,
                DisplayName = "Scales & Modes",
                Description = "Scale catalog filtered to modes that cover a selected chord's pitch content.",
                TotalItems = scaleCount,
                PrimaryMetric = "Modal family",
                Highlights = ["Major", "Minor", "Symmetrical"]
            }
        ];
    }

    private static string ComputeForteNumber(SetClass setClass)
    {
        var cardinality = setClass.Cardinality.Value;
        var icvCode = setClass.IntervalClassVector.Id.Value % 100;
        return $"{cardinality}-{icvCode:D2}";
    }

    private static string DescribeChordTemplate(ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.TonalModal tonal => tonal.Description,
            ChordTemplate.Analytical analytical => analytical.Description,
            _ => template.Name
        };
    }

    private static (string Frets, string Strings) FormatPositions(ImmutableList<Position> positions)
    {
        var fretParts = new List<string>();
        var stringParts = new List<string>();

        foreach (var position in positions)
        {
            switch (position)
            {
                case Position.Played played:
                    fretParts.Add($"{played.Location.Str.Value}:{played.Location.Fret.Value}");
                    stringParts.Add(played.Location.Str.ToString());
                    break;
                case Position.Muted muted:
                    fretParts.Add($"{muted.Str.Value}:X");
                    stringParts.Add(muted.Str.ToString());
                    break;
            }
        }

        return (string.Join(",", fretParts), string.Join(",", stringParts));
    }
}
