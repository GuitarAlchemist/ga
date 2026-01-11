namespace GA.Business.Core.Fretboard.Voicings.Search;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GA.Business.Core.Fretboard.Voicings.Search;

/// <summary>
/// Helper for high-performance binary serialization of VoicingDocuments.
/// Used to bypass slow Music Theory analysis on startup.
/// </summary>
public static class VoicingCacheSerialization
{
    private const int _formatVersion = 4;

    public static void SaveToCache(string filePath, IEnumerable<VoicingDocument> documents)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        using var writer = new BinaryWriter(fs);

        writer.Write(_formatVersion);

        var docsList = documents.ToList();
        writer.Write(docsList.Count);

        foreach (var doc in docsList)
        {
            WriteDocument(writer, doc);
        }
    }

    public static List<VoicingDocument> LoadFromCache(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
        using var reader = new BinaryReader(fs);

        var version = reader.ReadInt32();
        if (version != _formatVersion)
        {
            throw new InvalidOperationException($"Cache format version mismatch. Expected {_formatVersion}, got {version}");
        }

        var count = reader.ReadInt32();
        var results = new List<VoicingDocument>(count);

        for (var i = 0; i < count; i++)
        {
            results.Add(ReadDocument(reader));
        }

        return results;
    }

    private static void WriteDocument(BinaryWriter w, VoicingDocument doc)
    {
        // Core Strings
        w.Write(doc.Id);
        w.Write(doc.SearchableText);
        w.WriteNullableString(doc.ChordName);
        w.WriteNullableString(doc.VoicingType);
        w.WriteNullableString(doc.Position);
        w.WriteNullableString(doc.Difficulty);
        w.WriteNullableString(doc.ModeName);
        w.WriteNullableString(doc.ModalFamily);

        // Arrays
        w.WriteArray(doc.SemanticTags);
        w.WriteArray(doc.PossibleKeys);

        // Equivalence
        w.WriteNullableString(doc.PrimeFormId);
        w.Write(doc.TranslationOffset);

        // Heavy Data
        w.Write(doc.YamlAnalysis);
        w.Write(doc.Diagram);

        // Primitives
        w.WriteIntArray(doc.MidiNotes);
        w.WriteIntArray(doc.PitchClasses);
        w.Write(doc.PitchClassSet);
        w.Write(doc.IntervalClassVector);

        w.Write(doc.MinFret);
        w.Write(doc.MaxFret);
        w.Write(doc.HandStretch);
        w.Write(doc.BarreRequired);

        // Metadata
        w.Write(doc.AnalysisEngine);
        w.Write(doc.AnalysisVersion);
        w.WriteArray(doc.Jobs);

        // Identifiers
        w.Write(doc.TuningId);
        w.Write(doc.PitchClassSetId);

        // Musical Identity
        w.Write(doc.RootPitchClass.HasValue);
        if (doc.RootPitchClass.HasValue) w.Write(doc.RootPitchClass.Value);
        w.Write(doc.MidiBassNote);
        w.WriteNullableString(doc.StackingType);

        // Phase 3 Features
        w.WriteNullableString(doc.HarmonicFunction);
        w.Write(doc.IsNaturallyOccurring);
        w.Write(doc.IsRootless);
        w.Write(doc.HasGuideTones);
        w.Write(doc.Inversion);
        w.WriteArray(doc.OmittedTones ?? []);

        // AI Agent Features
        w.Write(doc.TopPitchClass ?? -1); // -1 for null
        w.WriteNullableString(doc.TexturalDescription);
        w.WriteArray(doc.DoubledTones ?? []);
        w.WriteArray(doc.AlternateNames ?? []);

        // Metrics
        w.Write(doc.Brightness);
        w.Write(doc.Consonance);
        w.Write(doc.Roughness);
        w.Write(doc.DifficultyScore);

        // Phase 7 Embeddings
        w.WriteDoubleArray(doc.Embedding);
        w.WriteDoubleArray(doc.TextEmbedding);
    }

    private static VoicingDocument ReadDocument(BinaryReader r)
    {
        return new VoicingDocument
        {
            Id = r.ReadString(),
            SearchableText = r.ReadString(),
            ChordName = r.ReadNullableString(),
            VoicingType = r.ReadNullableString(),
            Position = r.ReadNullableString(),
            Difficulty = r.ReadNullableString(),
            ModeName = r.ReadNullableString(),
            ModalFamily = r.ReadNullableString(),

            SemanticTags = r.ReadArray(),
            PossibleKeys = r.ReadArray(),

            PrimeFormId = r.ReadNullableString(),
            TranslationOffset = r.ReadInt32(),

            YamlAnalysis = r.ReadString(),
            Diagram = r.ReadString(),

            MidiNotes = r.ReadIntArray(),
            PitchClasses = r.ReadIntArray(),
            PitchClassSet = r.ReadString(),
            IntervalClassVector = r.ReadString(),

            MinFret = r.ReadInt32(),
            MaxFret = r.ReadInt32(),
            HandStretch = r.ReadInt32(),
            BarreRequired = r.ReadBoolean(),

            AnalysisEngine = r.ReadString(),
            AnalysisVersion = r.ReadString(),
            Jobs = r.ReadArray(),

            TuningId = r.ReadString(),
            PitchClassSetId = r.ReadString(),

            RootPitchClass = r.ReadBoolean() ? r.ReadInt32() : null,
            MidiBassNote = r.ReadInt32(),
            StackingType = r.ReadNullableString(),

            HarmonicFunction = r.ReadNullableString(),
            IsNaturallyOccurring = r.ReadBoolean(),
            IsRootless = r.ReadBoolean(),
            HasGuideTones = r.ReadBoolean(),
            Inversion = r.ReadInt32(),
            OmittedTones = r.ReadArray(),

            TopPitchClass = r.ReadInt32() is int tpc && tpc != -1 ? tpc : null,
            TexturalDescription = r.ReadNullableString(),
            DoubledTones = r.ReadArray(),
            AlternateNames = r.ReadArray(),

            Brightness = r.ReadDouble(),
            Consonance = r.ReadDouble(),
            Roughness = r.ReadDouble(),
            DifficultyScore = r.ReadDouble(),
            
            // Phase 7 Embeddings
            Embedding = r.ReadDoubleArray(),
            TextEmbedding = r.ReadDoubleArray()
        };
    }

    // --- Extension Logic ---

    private static void WriteNullableString(this BinaryWriter w, string? value)
    {
        w.Write(value != null);
        if (value != null) w.Write(value);
    }

    private static string? ReadNullableString(this BinaryReader r)
    {
        var hasValue = r.ReadBoolean();
        return hasValue ? r.ReadString() : null;
    }

    private static void WriteArray(this BinaryWriter w, string[] array)
    {
        w.Write(array.Length);
        foreach (var item in array) w.Write(item);
    }

    private static string[] ReadArray(this BinaryReader r)
    {
        var count = r.ReadInt32();
        var arr = new string[count];
        for (var i = 0; i < count; i++) arr[i] = r.ReadString();
        return arr;
    }

    private static void WriteIntArray(this BinaryWriter w, int[] array)
    {
        w.Write(array.Length);
        foreach (var item in array) w.Write(item);
    }

    private static int[] ReadIntArray(this BinaryReader r)
    {
        var count = r.ReadInt32();
        var arr = new int[count];
        for (var i = 0; i < count; i++) arr[i] = r.ReadInt32();
        return arr;
    }

    private static void WriteDoubleArray(this BinaryWriter w, double[]? array)
    {
        if (array == null)
        {
            w.Write(0);
            return;
        }
        w.Write(array.Length);
        foreach (var item in array) w.Write(item);
    }

    private static double[]? ReadDoubleArray(this BinaryReader r)
    {
        var count = r.ReadInt32();
        if (count == 0) return null;
        var arr = new double[count];
        for (var i = 0; i < count; i++) arr[i] = r.ReadDouble();
        return arr;
    }
}
