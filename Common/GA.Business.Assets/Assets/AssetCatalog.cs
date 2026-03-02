namespace GA.Business.Assets;

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using GA.Domain.Core.Instruments.Fingering;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal;
using GA.Domain.Core.Theory.Tonal.Primitives.Diatonic;
using GA.Domain.Core.Theory.Tonal.Scales;

[PublicAPI]
public static class AssetCatalog
{
    private static readonly Lazy<FrozenDictionary<string, Asset>> _indexedProperties = new(LoadAssets, true);
    public static IReadOnlyCollection<NaturalNote> NaturalNotes => NaturalNote.Items;
    public static IReadOnlyCollection<Note.Flat> FlatNotes => Note.Flat.Items;
    public static IReadOnlyCollection<Note.Sharp> SharpNotes => Note.Sharp.Items;
    public static IReadOnlyCollection<Note.Accidented> AccidentedNotes => Note.Accidented.Items;

    public static IReadOnlyCollection<KeySignature> KeySignatures => KeySignature.Items;
    public static IReadOnlyCollection<Key> Keys => Key.Items;

    public static IReadOnlyCollection<SimpleIntervalSize> IntervalSizes => SimpleIntervalSize.Items;
    public static IReadOnlyCollection<CompoundIntervalSize> CompoundIntervalSizes => CompoundIntervalSize.Items;
    public static IReadOnlyCollection<PitchClass> PitchClasses => PitchClass.Items;
    public static IReadOnlyCollection<IntervalClass> IntervalClasses => IntervalClass.Items;
    public static IReadOnlyCollection<PitchClassSetId> PitchClassSetIds => PitchClassSetId.Items;

    public static IReadOnlyCollection<Cardinality> Cardinalities => Cardinality.Items;

    public static IReadOnlyCollection<MajorScaleDegree> MajorScaleDegrees => MajorScaleDegree.Items;

    public static IReadOnlyCollection<NaturalMinorScaleDegree> NaturalMinorScaleDegrees =>
        NaturalMinorScaleDegree.Items;

    public static IReadOnlyCollection<HarmonicMinorScaleDegree> HarmonicMinorScaleDegrees =>
        HarmonicMinorScaleDegree.Items;

    public static IReadOnlyCollection<MelodicMinorScaleDegree> MelodicMinorScaleDegrees =>
        MelodicMinorScaleDegree.Items;

    public static IReadOnlyCollection<Scale> Scales => Scale.Items;
    public static IReadOnlyCollection<ModalFamily> ModalFamilies => ModalFamily.Items;

    public static IReadOnlyCollection<Finger> Fingers => Finger.Items;
    public static IReadOnlyCollection<FingerCount> FingerCounts => FingerCount.Items;
    public static IReadOnlyCollection<Fret> Frets => Fret.Items;
    public static IReadOnlyCollection<RelativeFret> RelativeFrets => RelativeFret.Items;

    public static IEnumerable<string> Names => _indexedProperties.Value.Keys;
    public static IReadOnlyDictionary<string, Asset> AssetByName => _indexedProperties.Value;

    public static Asset Get(string name) => _indexedProperties.Value.TryGetValue(name, out var asset)
            ? asset
            : throw new KeyNotFoundException($"Asset {name} not found.");

    public static string Print()
    {
        var sb = new StringBuilder();
        var names = Names;
        foreach (var assetName in names)
        {
            var asset = Get(assetName);
            sb.AppendLine($"=== {assetName}");
            sb.AppendLine(string.Join(Environment.NewLine, asset.Items));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static FrozenDictionary<string, Asset> LoadAssets()
    {
        var builder = new Dictionary<string, Asset>();
        var properties =
            typeof(AssetCatalog)
                .GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(prop =>
                    prop.PropertyType.IsGenericType &&
                    prop.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))
                .Select(prop => (Property: prop, ItemType: prop.PropertyType.GetGenericArguments()[0]))
                .OrderBy(x => x.ItemType.Namespace)
                .ThenBy(x => x.ItemType.Name)
                .ToImmutableList();

        foreach (var (property, itemType) in properties)
        {
            var name = property.Name;
            var items = property.GetValue(null);
            var assetType = typeof(Asset<>).MakeGenericType(itemType);
            var asset = Activator.CreateInstance(assetType, name, items);
            if (asset is Asset typedAsset)
            {
                builder.Add(name, typedAsset);
            }
        }

        return builder.ToFrozenDictionary();
    }
}
