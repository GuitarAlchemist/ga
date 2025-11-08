namespace GaApi.Models;

/// <summary>
///     Response model for BSP spatial queries
/// </summary>
public class BspSpatialQueryResponse
{
    /// <summary>
    ///     The original query chord
    /// </summary>
    public string QueryChord { get; set; } = "";

    /// <summary>
    ///     Search radius used
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    ///     Partition strategy used
    /// </summary>
    public string Strategy { get; set; } = "";

    /// <summary>
    ///     The tonal region found
    /// </summary>
    public BspRegionDto Region { get; set; } = new();

    /// <summary>
    ///     Elements found in the spatial query
    /// </summary>
    public List<BspElementDto> Elements { get; set; } = [];

    /// <summary>
    ///     Confidence score of the result
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    ///     Query execution time in milliseconds
    /// </summary>
    public double QueryTimeMs { get; set; }
}

/// <summary>
///     Response model for tonal context queries
/// </summary>
public class BspTonalContextResponse
{
    /// <summary>
    ///     The original query chord
    /// </summary>
    public string QueryChord { get; set; } = "";

    /// <summary>
    ///     The tonal region found
    /// </summary>
    public BspRegionDto Region { get; set; } = new();

    /// <summary>
    ///     Confidence score of the result
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    ///     Query execution time in milliseconds
    /// </summary>
    public double QueryTimeMs { get; set; }

    /// <summary>
    ///     Detailed analysis of the chord in context
    /// </summary>
    public BspAnalysisDto Analysis { get; set; } = new();
}

/// <summary>
///     Request model for progression analysis
/// </summary>
public class BspProgressionRequest
{
    /// <summary>
    ///     List of chords in the progression
    /// </summary>
    public List<BspChordRequest> Chords { get; set; } = [];
}

/// <summary>
///     Individual chord in a progression request
/// </summary>
public class BspChordRequest
{
    /// <summary>
    ///     Chord name (e.g., "C Major")
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Comma-separated pitch classes (e.g., "C,E,G")
    /// </summary>
    public string PitchClasses { get; set; } = "";
}

/// <summary>
///     Response model for progression analysis
/// </summary>
public class BspProgressionAnalysisResponse
{
    /// <summary>
    ///     The chord progression analyzed
    /// </summary>
    public List<string> Progression { get; set; } = [];

    /// <summary>
    ///     Analysis of each chord in the progression
    /// </summary>
    public List<BspChordAnalysisDto> ChordAnalyses { get; set; } = [];

    /// <summary>
    ///     Analysis of transitions between chords
    /// </summary>
    public List<BspTransitionDto> Transitions { get; set; } = [];

    /// <summary>
    ///     Overall progression analysis
    /// </summary>
    public BspOverallAnalysisDto OverallAnalysis { get; set; } = new();
}

/// <summary>
///     BSP region data transfer object
/// </summary>
public class BspRegionDto
{
    /// <summary>
    ///     Region name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Tonality type (Major, Minor, etc.)
    /// </summary>
    public string TonalityType { get; set; } = "";

    /// <summary>
    ///     Tonal center (root note)
    /// </summary>
    public int TonalCenter { get; set; }

    /// <summary>
    ///     Pitch classes in the region
    /// </summary>
    public List<string> PitchClasses { get; set; } = [];
}

/// <summary>
///     BSP element data transfer object
/// </summary>
public class BspElementDto
{
    /// <summary>
    ///     Element name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Tonality type
    /// </summary>
    public string TonalityType { get; set; } = "";

    /// <summary>
    ///     Tonal center
    /// </summary>
    public double TonalCenter { get; set; }

    /// <summary>
    ///     Pitch classes in the element
    /// </summary>
    public List<string> PitchClasses { get; set; } = [];
}

/// <summary>
///     BSP analysis data transfer object
/// </summary>
public class BspAnalysisDto
{
    /// <summary>
    ///     Whether the chord is fully contained in the region
    /// </summary>
    public bool ContainedInRegion { get; set; }

    /// <summary>
    ///     Number of common tones with the region
    /// </summary>
    public int CommonTones { get; set; }

    /// <summary>
    ///     Total number of tones in the chord
    /// </summary>
    public int TotalTones { get; set; }

    /// <summary>
    ///     Percentage of chord tones that fit in the region
    /// </summary>
    public double FitPercentage { get; set; }
}

/// <summary>
///     Individual chord analysis in a progression
/// </summary>
public class BspChordAnalysisDto
{
    /// <summary>
    ///     Chord name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Pitch classes
    /// </summary>
    public string PitchClasses { get; set; } = "";

    /// <summary>
    ///     Tonal region
    /// </summary>
    public BspRegionDto Region { get; set; } = new();

    /// <summary>
    ///     Confidence score
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    ///     Query time in milliseconds
    /// </summary>
    public double QueryTimeMs { get; set; }
}

/// <summary>
///     Transition analysis between two chords
/// </summary>
public class BspTransitionDto
{
    /// <summary>
    ///     Source chord name
    /// </summary>
    public string FromChord { get; set; } = "";

    /// <summary>
    ///     Target chord name
    /// </summary>
    public string ToChord { get; set; } = "";

    /// <summary>
    ///     Spatial distance between chords
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    ///     Number of common tones
    /// </summary>
    public int CommonTones { get; set; }

    /// <summary>
    ///     Smoothness score (1 - distance)
    /// </summary>
    public double Smoothness { get; set; }
}

/// <summary>
///     Overall progression analysis
/// </summary>
public class BspOverallAnalysisDto
{
    /// <summary>
    ///     Average confidence across all chords
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    ///     Average distance between adjacent chords
    /// </summary>
    public double AverageDistance { get; set; }

    /// <summary>
    ///     Average smoothness of transitions
    /// </summary>
    public double AverageSmoothness { get; set; }

    /// <summary>
    ///     Total common tones across all transitions
    /// </summary>
    public int TotalCommonTones { get; set; }

    /// <summary>
    ///     Number of chords in the progression
    /// </summary>
    public int ProgressionLength { get; set; }
}

/// <summary>
///     BSP tree information response
/// </summary>
public class BspTreeInfoResponse
{
    /// <summary>
    ///     Root region name
    /// </summary>
    public string RootRegion { get; set; } = "";

    /// <summary>
    ///     Total number of regions in the tree
    /// </summary>
    public int TotalRegions { get; set; }

    /// <summary>
    ///     Maximum depth of the tree
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    ///     Available partition strategies
    /// </summary>
    public List<string> PartitionStrategies { get; set; } = [];

    /// <summary>
    ///     Supported operations
    /// </summary>
    public List<string> SupportedOperations { get; set; } = [];
}

/// <summary>
///     Full BSP tree structure response
/// </summary>
public class BspTreeStructureResponse
{
    /// <summary>
    ///     Root node of the BSP tree
    /// </summary>
    public BspNodeDto Root { get; set; } = new();

    /// <summary>
    ///     Total number of nodes
    /// </summary>
    public int NodeCount { get; set; }

    /// <summary>
    ///     Maximum depth
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    ///     Number of leaf regions
    /// </summary>
    public int RegionCount { get; set; }

    /// <summary>
    ///     Number of partition planes
    /// </summary>
    public int PartitionCount { get; set; }
}

/// <summary>
///     BSP node data transfer object
/// </summary>
public class BspNodeDto
{
    /// <summary>
    ///     Region information
    /// </summary>
    public BspRegionDto Region { get; set; } = new();

    /// <summary>
    ///     Partition plane (null for leaf nodes)
    /// </summary>
    public BspPartitionDto? Partition { get; set; }

    /// <summary>
    ///     Left child node
    /// </summary>
    public BspNodeDto? Left { get; set; }

    /// <summary>
    ///     Right child node
    /// </summary>
    public BspNodeDto? Right { get; set; }

    /// <summary>
    ///     Whether this is a leaf node
    /// </summary>
    public bool IsLeaf { get; set; }

    /// <summary>
    ///     Depth in the tree
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    ///     Elements in this region (for leaf nodes)
    /// </summary>
    public List<BspElementDto> Elements { get; set; } = [];
}

/// <summary>
///     BSP partition plane data transfer object
/// </summary>
public class BspPartitionDto
{
    /// <summary>
    ///     Partition strategy used
    /// </summary>
    public string Strategy { get; set; } = "";

    /// <summary>
    ///     Reference point (tonal center)
    /// </summary>
    public double ReferencePoint { get; set; }

    /// <summary>
    ///     Threshold value
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    ///     Normal vector (for visualization)
    /// </summary>
    public List<double> Normal { get; set; } = [];
}

// ============================================================================
// BSP Room Generation Models
// ============================================================================

/// <summary>
///     Room data transfer object
/// </summary>
public class RoomDto
{
    /// <summary>
    ///     X coordinate of the room
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Y coordinate of the room
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Width of the room
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of the room
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Center X coordinate
    /// </summary>
    public int CenterX { get; set; }

    /// <summary>
    ///     Center Y coordinate
    /// </summary>
    public int CenterY { get; set; }
}

/// <summary>
///     Corridor data transfer object
/// </summary>
public class CorridorDto
{
    /// <summary>
    ///     Points defining the corridor path
    /// </summary>
    public List<PointDto> Points { get; set; } = [];

    /// <summary>
    ///     Width of the corridor
    /// </summary>
    public int Width { get; set; }
}

/// <summary>
///     2D point data transfer object
/// </summary>
public class PointDto
{
    /// <summary>
    ///     X coordinate
    /// </summary>
    public float X { get; set; }

    /// <summary>
    ///     Y coordinate
    /// </summary>
    public float Y { get; set; }
}

/// <summary>
///     Complete dungeon layout response
/// </summary>
public class DungeonLayoutResponse
{
    /// <summary>
    ///     Width of the dungeon
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of the dungeon
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     List of rooms in the dungeon
    /// </summary>
    public List<RoomDto> Rooms { get; set; } = [];

    /// <summary>
    ///     List of corridors connecting rooms
    /// </summary>
    public List<CorridorDto> Corridors { get; set; } = [];

    /// <summary>
    ///     Seed used for generation (for reproducibility)
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    ///     Generation parameters used
    /// </summary>
    public DungeonGenerationParams Params { get; set; } = new();
}

/// <summary>
///     Parameters for dungeon generation
/// </summary>
public class DungeonGenerationParams
{
    /// <summary>
    ///     Width of the dungeon
    /// </summary>
    public int Width { get; set; } = 80;

    /// <summary>
    ///     Height of the dungeon
    /// </summary>
    public int Height { get; set; } = 60;

    /// <summary>
    ///     Maximum BSP tree depth
    /// </summary>
    public int MaxDepth { get; set; } = 4;

    /// <summary>
    ///     Minimum room size
    /// </summary>
    public int MinRoomSize { get; set; } = 6;

    /// <summary>
    ///     Maximum room size
    /// </summary>
    public int MaxRoomSize { get; set; } = 12;

    /// <summary>
    ///     Corridor width
    /// </summary>
    public int CorridorWidth { get; set; } = 1;

    /// <summary>
    ///     Optional seed for reproducible generation
    /// </summary>
    public int? Seed { get; set; }
}

// ============================================================================
// Music Theory Room Generation Models
// ============================================================================

/// <summary>
///     Music room data transfer object with music theory data
/// </summary>
public class MusicRoomDto
{
    /// <summary>
    ///     Unique room identifier
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     X coordinate of the room
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Y coordinate of the room
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Width of the room
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of the room
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Center X coordinate
    /// </summary>
    public int CenterX { get; set; }

    /// <summary>
    ///     Center Y coordinate
    /// </summary>
    public int CenterY { get; set; }

    /// <summary>
    ///     Floor number (0-5)
    /// </summary>
    public int Floor { get; set; }

    /// <summary>
    ///     Music category (e.g., "Major", "Minor", "Jazz Voicings")
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    ///     List of music items in this room
    /// </summary>
    public List<string> Items { get; set; } = [];

    /// <summary>
    ///     Color for this room (CSS color string)
    /// </summary>
    public string Color { get; set; } = "";

    /// <summary>
    ///     Room description
    /// </summary>
    public string Description { get; set; } = "";
}

/// <summary>
///     Complete music floor response
/// </summary>
public class MusicFloorResponse
{
    /// <summary>
    ///     Floor number (0-5)
    /// </summary>
    public int Floor { get; set; }

    /// <summary>
    ///     Floor name (e.g., "Set Classes", "Forte Codes")
    /// </summary>
    public string FloorName { get; set; } = "";

    /// <summary>
    ///     Size of the floor
    /// </summary>
    public int FloorSize { get; set; }

    /// <summary>
    ///     Total number of music items on this floor
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    ///     Music categories on this floor
    /// </summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>
    ///     List of music rooms
    /// </summary>
    public List<MusicRoomDto> Rooms { get; set; } = [];

    /// <summary>
    ///     List of corridors connecting rooms
    /// </summary>
    public List<CorridorDto> Corridors { get; set; } = [];

    /// <summary>
    ///     Seed used for generation (for reproducibility)
    /// </summary>
    public int? Seed { get; set; }
}
