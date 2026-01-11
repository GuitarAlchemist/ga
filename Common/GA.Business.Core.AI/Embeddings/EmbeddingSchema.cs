namespace GA.Business.Core.AI.Embeddings;

/// <summary>
/// Canonical definition of the Musical Embedding Vector Schema (v1.2.1).
/// This schema aligns internal 'Structure' with music-theoretic OPTIC/K Equivalence.
/// Total Dimension: 96
/// </summary>
public static class EmbeddingSchema
{
    public const string Version = "OPTIC-K-v1.2.1";
    public const int TotalDimension = 96;

    // 1. Identity (O)
    public const int IdentityOffset = 0;
    public const int IdentityDim = 6;

    // 2. Structure (T) - OPTIC/K Core
    public const int StructureOffset = 6;
    public const int StructureDim = 24;

    // 3. Morphology (P) - Physical/Fretboard
    public const int MorphologyOffset = 30;
    public const int MorphologyDim = 24;

    // 4. Context (C) - Progressive Role
    public const int ContextOffset = 54;
    public const int ContextDim = 12;

    // 5. Symbolic (K) - Knowledge/Tags
    public const int SymbolicOffset = 66;
    public const int SymbolicDim = 12;

    // 6. Extensions (v1.2.1)
    public const int ExtensionsOffset = 78;
    public const int ExtensionsDim = 18;

    // Sub-ranges for Knowledge
    public const int KnowledgeTechniqueDim = 6; // First 6
    public const int KnowledgeStyleDim = 6;     // Next 6

    // Psychoacoustic Constants
    public const int MinMidi = 40;  // E2
    public const int MaxMidi = 88;  // E6
    public const int LowThreshold = 52; // E3
    public const int HighThreshold = 76; // E5
    public const double SpreadMax = 12.0;
    public const double SpanMax = 48.0; // 4 octaves

    // Feature Indices - Context Dynamics
    public const int HarmonicInertia = 78;
    public const int ResolutionPressure = 79;

    // Feature Indices - Textural Features
    public const int Textural_DoublingRatio = 80;
    public const int Textural_RootDoubled = 81;
    public const int Textural_TopNoteRelative = 82;

    // Feature Indices - Relational
    public const int Relational_SmoothnessBudget = 83;

    // Feature Indices - Spectral Color
    public const int Spectral_MeanRegister = 84;
    public const int Spectral_RegisterSpread = 85;
    public const int Spectral_LowEndWeight = 86;
    public const int Spectral_HighEndWeight = 87;
    public const int Spectral_LocalClustering = 88;
    public const int Spectral_RoughnessProxy = 89;

    // Feature Indices - Extended Texture (v1.2.1)
    public const int Extended_BassMelodySpan = 90;
    public const int Extended_ThirdDoubled = 91;
    public const int Extended_FifthDoubled = 92;
    public const int Extended_OpenPosition = 93;
    public const int Extended_InnerVoiceDensity = 94;
    public const int Extended_OmittedRoot = 95;
}
