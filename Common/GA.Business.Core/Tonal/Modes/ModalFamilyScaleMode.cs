﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿namespace GA.Business.Core.Tonal.Modes;

using Atonal;
using Config;
using Intervals;
using Notes;
using Primitives;
using Scales;

/// <summary>
/// Represents a mode derived from a modal family of scales. A modal family is a collection of scales
/// that share the same interval class vector (a measure of the intervallic content).
/// For example, the major scale family includes modes like Ionian, Dorian, Phrygian, etc.,
/// all sharing the interval vector &lt;2 5 4 3 6 1&gt;
/// </summary>
/// <remarks>
/// Each mode is identified by:
/// - Its parent modal family (e.g., Major scale family)
/// - Its degree within that family (e.g., 4 for Lydian mode)
/// - Its collection of notes and intervals
/// The class provides functionality to analyze modal characteristics like intervals, color tones,
/// and relationships to other modes in the same family.
///
/// This class provides factory methods for creating and accessing modal family scale modes
/// similar to other scale mode classes.
/// </remarks>
[PublicAPI]
public class ModalFamilyScaleMode : ScaleMode
{
    private readonly Lazy<ModeFormula> _lazyModeFormula;
    private readonly Lazy<IReadOnlyCollection<Note>> _lazyColorNotes;
    private static readonly Lazy<ImmutableDictionary<int, ModalFamilyScaleMode>> _lazyModeByDegree =
        new(() => Items.ToImmutableDictionary(mode => mode.Degree));

    public ModalFamilyScaleMode(
        ModalFamily modalFamily,
        int degree,
        IReadOnlyCollection<Note> notes,
        ModesConfigCache.ModeCacheValue? modeConfig)
        : base(new Scale(notes))  // Pass a new Scale instance created from notes
    {
        ModalFamily = modalFamily ?? throw new ArgumentNullException(nameof(modalFamily));
        Degree = degree;

        Notes = notes;
        PitchClassSet = new PitchClassSet(notes.Select(n => n.PitchClass));
        SimpleIntervals = CalculateSimpleIntervals();
        ModeConfig = modeConfig;
        // We're using the base Scale constructor, so we don't need to set ParentScale
        // ParentScale = null;

        _lazyModeFormula = new(() => new(this));
        _lazyColorNotes = new(CalculateCharacteristicNotes);
    }

    public ModalFamily ModalFamily { get; }
    public int Degree { get; }
    public override IReadOnlyCollection<Note> Notes { get; }
    public PitchClassSet PitchClassSet { get; }
    public override IReadOnlyCollection<Interval.Simple> SimpleIntervals { get; }
    public override string Name => ModeConfig?.Mode.Name ?? $"Mode {Degree} of {ModalFamily}";

    public ModesConfigCache.ModeCacheValue? ModeConfig { get; }

    /// <summary>
    /// Gets the characteristic notes of this mode
    /// </summary>
    /// <remarks>
    /// This property hides the base class property of the same name.
    /// </remarks>
    public new IReadOnlyCollection<Note> CharacteristicNotes => _lazyColorNotes.Value;

    /// <summary>
    /// Gets all instances of modal family scale modes
    /// </summary>
    public static IEnumerable<ModalFamilyScaleMode> Items => ModalFamilyScaleModeFactory.CreateModesFromAllFamilies();

    /// <summary>
    /// Gets a modal family scale mode by its degree
    /// </summary>
    /// <param name="degree">The degree</param>
    /// <returns>The modal family scale mode</returns>
    public static ModalFamilyScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];

    /// <summary>
    /// Creates a modal family scale mode from a scale and degree
    /// </summary>
    /// <param name="scale">The scale</param>
    /// <param name="degree">The degree</param>
    /// <returns>The modal family scale mode, or null if the scale is not modal or the degree is invalid</returns>
    /// <exception cref="InvalidOperationException">Thrown when rotation fails</exception>
    public static ModalFamilyScaleMode? FromScale(Scale scale, int degree)
    {
        if (!scale.IsModal || scale.ModalFamily == null) return null;
        if (degree < 1 || degree > scale.Count) return null;

        var rotatedNotes = scale.Rotate(degree - 1);
        if (rotatedNotes.Count != scale.Count) throw new InvalidOperationException($"Rotation error: Expected {scale.Count} notes but got {rotatedNotes.Count}");

        var pitchClassSet = new PitchClassSet(rotatedNotes.Select(note => note.PitchClass));
        var id = pitchClassSet.Id.Value;

        ModesConfigCache.ModeCacheValue? modeConfig = null;
        ModesConfigCache.Instance.TryGetModeByPitchClassSetId(id, out modeConfig);

        return new ModalFamilyScaleMode(scale.ModalFamily, degree, rotatedNotes, modeConfig);
    }

    private ImmutableList<Interval.Simple> CalculateSimpleIntervals()
    {
        var rootNote = Notes.First();
        return Notes
            .Select(note => rootNote.GetInterval(note))
            .ToImmutableList();
    }

    private ImmutableList<Note> CalculateCharacteristicNotes()
    {
        var characteristicIntervals = Formula.CharacteristicIntervals;
        var rootNote = Notes.First();
        var tuples = Notes.Select(note =>
            (Note: note, rootNote.GetInterval(note).Semitones));

        var noteBySemitones = tuples.ToImmutableDictionary(
            tuple => tuple.Semitones,
            tuple => tuple.Note);

        return characteristicIntervals
            .Select(interval => noteBySemitones[interval.ToSemitones()])
            .ToImmutableList();
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} - {Formula}";
}