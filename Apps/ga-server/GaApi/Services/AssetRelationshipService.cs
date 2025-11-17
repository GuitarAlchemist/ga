namespace GaApi.Services;

using Models;

/// <summary>
///     Service for managing musical asset relationships and hierarchies
/// </summary>
public class AssetRelationshipService : IAssetRelationshipService
{
    private readonly Dictionary<string, string> _assetDisplayNames;
    private readonly Dictionary<string, AssetNodeUiMetadata> _assetUiMetadata;
    private readonly List<AssetRelationship> _relationships;

    public AssetRelationshipService()
    {
        _assetDisplayNames = InitializeAssetDisplayNames();
        _assetUiMetadata = InitializeAssetUiMetadata();
        _relationships = InitializeRelationships();
    }

    public List<AssetRelationship> GetAllRelationships()
    {
        return _relationships;
    }

    public List<AssetRelationship> GetRelationshipsForAsset(string assetType)
    {
        return [.. _relationships.Where(r =>
                r.ParentAssetType == assetType ||
                (r.IsBidirectional && r.ChildAssetType == assetType))];
    }

    public List<string> GetChildAssetTypes(string parentAssetType)
    {
        return [.. _relationships
            .Where(r => r.ParentAssetType == parentAssetType)
            .Select(r => r.ChildAssetType)
            .Distinct()];
    }

    public List<string> GetParentAssetTypes(string childAssetType)
    {
        return [.. _relationships
            .Where(r => r.ChildAssetType == childAssetType ||
                        (r.IsBidirectional && r.ParentAssetType == childAssetType))
            .Select(r => r.ParentAssetType)
            .Distinct()];
    }

    public AssetHierarchyNode BuildAssetHierarchy()
    {
        // Start with fundamental music theory concepts
        var root = new AssetHierarchyNode
        {
            AssetType = "MusicTheory",
            DisplayName = "Music Theory",
            Description = "Fundamental concepts of music theory and harmony",
            UiMetadata = new AssetNodeUiMetadata
            {
                Icon = "fas fa-music",
                Color = "primary",
                ExpandedByDefault = true,
                DisplayOrder = 0
            }
        };

        // Build the hierarchy recursively
        BuildHierarchyRecursive(root, []);

        return root;
    }

    public List<AssetRelationship> GetRelationshipPath(string fromAssetType, string toAssetType)
    {
        // Simple implementation - could be enhanced with graph algorithms
        var directRelationship = _relationships.FirstOrDefault(r =>
            (r.ParentAssetType == fromAssetType && r.ChildAssetType == toAssetType) ||
            (r.IsBidirectional && r.ParentAssetType == toAssetType && r.ChildAssetType == fromAssetType));

        return directRelationship != null ? [directRelationship] : [];
    }

    private void BuildHierarchyRecursive(AssetHierarchyNode node, HashSet<string> visited)
    {
        if (visited.Contains(node.AssetType))
        {
            return;
        }

        visited.Add(node.AssetType);

        var childRelationships = _relationships
            .Where(r => r.ParentAssetType == node.AssetType)
            .OrderBy(r => r.UiHints.DisplayOrder)
            .ToList();

        foreach (var relationship in childRelationships)
        {
            var childNode = new AssetHierarchyNode
            {
                AssetType = relationship.ChildAssetType,
                DisplayName =
                    _assetDisplayNames.GetValueOrDefault(relationship.ChildAssetType, relationship.ChildAssetType),
                Description = relationship.Description,
                UiMetadata = _assetUiMetadata.GetValueOrDefault(relationship.ChildAssetType, new AssetNodeUiMetadata()),
                HasData = IsAssetTypeAvailable(relationship.ChildAssetType)
            };

            node.Children.Add(childNode);
            node.ChildRelationships.Add(relationship);

            // Recursively build children (with cycle detection)
            BuildHierarchyRecursive(childNode, [..visited]);
        }
    }

    private bool IsAssetTypeAvailable(string assetType)
    {
        // These are the asset types we know are available in the system
        var availableAssets = new HashSet<string>
        {
            "ChordTemplates", "Scales", "Keys", "KeySignatures",
            "NaturalNotes", "FlatNotes", "SharpNotes", "PitchClasses",
            "IntervalClasses", "MajorScaleDegrees", "NaturalMinorScaleDegrees",
            "HarmonicMinorScaleDegrees", "MelodicMinorScaleDegrees",
            "ModalFamilies", "Fingers", "Frets"
        };

        return availableAssets.Contains(assetType);
    }

    private Dictionary<string, string> InitializeAssetDisplayNames()
    {
        return new Dictionary<string, string>
        {
            // Core Theory
            ["PitchClasses"] = "Pitch Classes",
            ["IntervalClasses"] = "Interval Classes",
            ["NaturalNotes"] = "Natural Notes",
            ["FlatNotes"] = "Flat Notes",
            ["SharpNotes"] = "Sharp Notes",

            // Scales and Modes
            ["Scales"] = "Scales",
            ["ModalFamilies"] = "Modal Families",
            ["MajorScaleDegrees"] = "Major Scale Degrees",
            ["NaturalMinorScaleDegrees"] = "Natural Minor Scale Degrees",
            ["HarmonicMinorScaleDegrees"] = "Harmonic Minor Scale Degrees",
            ["MelodicMinorScaleDegrees"] = "Melodic Minor Scale Degrees",

            // Harmony
            ["ChordTemplates"] = "Chord Templates",
            ["Keys"] = "Keys",
            ["KeySignatures"] = "Key Signatures",

            // Instrument-Specific
            ["Fingers"] = "Guitar Fingers",
            ["Frets"] = "Guitar Frets"
        };
    }

    private Dictionary<string, AssetNodeUiMetadata> InitializeAssetUiMetadata()
    {
        return new Dictionary<string, AssetNodeUiMetadata>
        {
            ["PitchClasses"] = new()
                { Icon = "fas fa-circle", Color = "primary", DisplayOrder = 1, SupportsFiltering = false },
            ["IntervalClasses"] = new() { Icon = "fas fa-arrows-alt-h", Color = "info", DisplayOrder = 2 },
            ["NaturalNotes"] = new()
                { Icon = "fas fa-music", Color = "success", DisplayOrder = 3, SupportsFiltering = false },
            ["FlatNotes"] = new()
                { Icon = "fas fa-music", Color = "warning", DisplayOrder = 4, SupportsFiltering = false },
            ["SharpNotes"] = new()
                { Icon = "fas fa-music", Color = "danger", DisplayOrder = 5, SupportsFiltering = false },

            ["Scales"] = new()
                { Icon = "fas fa-stairs", Color = "primary", DisplayOrder = 10, ExpandedByDefault = true },
            ["ModalFamilies"] = new() { Icon = "fas fa-layer-group", Color = "secondary", DisplayOrder = 11 },
            ["MajorScaleDegrees"] = new() { Icon = "fas fa-sort-numeric-up", Color = "success", DisplayOrder = 12 },
            ["NaturalMinorScaleDegrees"] = new() { Icon = "fas fa-sort-numeric-up", Color = "info", DisplayOrder = 13 },
            ["HarmonicMinorScaleDegrees"] = new()
                { Icon = "fas fa-sort-numeric-up", Color = "warning", DisplayOrder = 14 },
            ["MelodicMinorScaleDegrees"] = new()
                { Icon = "fas fa-sort-numeric-up", Color = "danger", DisplayOrder = 15 },

            ["ChordTemplates"] = new()
            {
                Icon = "fas fa-layer-group",
                Color = "primary",
                DisplayOrder = 20,
                ExpandedByDefault = true,
                AvailableFilters = ["Quality", "Extension", "StackingType"],
                AvailableSortOptions = ["Name", "Quality", "Extension", "NoteCount"]
            },
            ["Keys"] = new() { Icon = "fas fa-key", Color = "warning", DisplayOrder = 21 },
            ["KeySignatures"] = new() { Icon = "fas fa-signature", Color = "info", DisplayOrder = 22 },

            ["Fingers"] = new()
                { Icon = "fas fa-hand", Color = "secondary", DisplayOrder = 30, SupportsFiltering = false },
            ["Frets"] = new()
                { Icon = "fas fa-grip-lines", Color = "secondary", DisplayOrder = 31, SupportsFiltering = false }
        };
    }

    private List<AssetRelationship> InitializeRelationships()
    {
        return
        [
            new()
            {
                ParentAssetType = "MusicTheory",
                ChildAssetType = "PitchClasses",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "The 12 fundamental pitch classes in equal temperament",
                Cardinality = "1:12",
                UiHints = new() { DisplayOrder = 1, ExpandedByDefault = true }
            },


            new()
            {
                ParentAssetType = "PitchClasses",
                ChildAssetType = "NaturalNotes",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Natural notes without accidentals",
                Cardinality = "1:7",
                UiHints = new() { DisplayOrder = 1 }
            },


            new()
            {
                ParentAssetType = "PitchClasses",
                ChildAssetType = "FlatNotes",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Notes with flat accidentals",
                Cardinality = "1:5",
                UiHints = new() { DisplayOrder = 2 }
            },


            new()
            {
                ParentAssetType = "PitchClasses",
                ChildAssetType = "SharpNotes",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Notes with sharp accidentals",
                Cardinality = "1:5",
                UiHints = new() { DisplayOrder = 3 }
            },

            // Interval relationships

            new()
            {
                ParentAssetType = "MusicTheory",
                ChildAssetType = "IntervalClasses",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "The fundamental interval classes used in music theory",
                Cardinality = "1:12",
                UiHints = new() { DisplayOrder = 2 }
            },

            // Scale relationships

            new()
            {
                ParentAssetType = "MusicTheory",
                ChildAssetType = "Scales",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Musical scales and their structures",
                Cardinality = "1:many",
                UiHints = new() { DisplayOrder = 10, ExpandedByDefault = true }
            },


            new()
            {
                ParentAssetType = "Scales",
                ChildAssetType = "ModalFamilies",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Modal families derived from scales",
                Cardinality = "1:many",
                UiHints = new() { DisplayOrder = 1 }
            },


            new()
            {
                ParentAssetType = "Scales",
                ChildAssetType = "MajorScaleDegrees",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Scale degrees in major scales",
                Cardinality = "1:7",
                UiHints = new() { DisplayOrder = 2 }
            },


            new()
            {
                ParentAssetType = "Scales",
                ChildAssetType = "NaturalMinorScaleDegrees",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Scale degrees in natural minor scales",
                Cardinality = "1:7",
                UiHints = new() { DisplayOrder = 3 }
            },


            new()
            {
                ParentAssetType = "Scales",
                ChildAssetType = "HarmonicMinorScaleDegrees",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Scale degrees in harmonic minor scales",
                Cardinality = "1:7",
                UiHints = new() { DisplayOrder = 4 }
            },


            new()
            {
                ParentAssetType = "Scales",
                ChildAssetType = "MelodicMinorScaleDegrees",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Scale degrees in melodic minor scales",
                Cardinality = "1:7",
                UiHints = new() { DisplayOrder = 5 }
            },

            // Harmony relationships

            new()
            {
                ParentAssetType = "MusicTheory",
                ChildAssetType = "ChordTemplates",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Chord templates and harmonic structures",
                Cardinality = "1:many",
                UiHints = new() { DisplayOrder = 20, ExpandedByDefault = true }
            },


            new()
            {
                ParentAssetType = "ChordTemplates",
                ChildAssetType = "Keys",
                RelationshipType = AssetRelationshipType.RelatedTo,
                Description = "Chords are used within musical keys",
                Cardinality = "many:many",
                IsBidirectional = true,
                UiHints = new() { DisplayOrder = 1 }
            },


            new()
            {
                ParentAssetType = "Keys",
                ChildAssetType = "KeySignatures",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Key signatures define the accidentals for keys",
                Cardinality = "1:1",
                UiHints = new() { DisplayOrder = 1 }
            },

            // Guitar-specific relationships

            new()
            {
                ParentAssetType = "MusicTheory",
                ChildAssetType = "Fingers",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Guitar fingering positions",
                Cardinality = "1:4",
                UiHints = new() { DisplayOrder = 30 }
            },


            new()
            {
                ParentAssetType = "MusicTheory",
                ChildAssetType = "Frets",
                RelationshipType = AssetRelationshipType.Contains,
                Description = "Guitar fret positions",
                Cardinality = "1:24",
                UiHints = new() { DisplayOrder = 31 }
            },

            // Cross-domain relationships

            new()
            {
                ParentAssetType = "IntervalClasses",
                ChildAssetType = "ChordTemplates",
                RelationshipType = AssetRelationshipType.BuildsInto,
                Description = "Intervals are used to construct chord templates",
                Cardinality = "many:many",
                UiHints = new() { DisplayOrder = 1, ShowInTreeView = false }
            },


            new()
            {
                ParentAssetType = "Scales",
                ChildAssetType = "ChordTemplates",
                RelationshipType = AssetRelationshipType.BuildsInto,
                Description = "Scales generate chord progressions and harmonies",
                Cardinality = "1:many",
                UiHints = new() { DisplayOrder = 2, ShowInTreeView = false }
            }
        ];
    }
}
