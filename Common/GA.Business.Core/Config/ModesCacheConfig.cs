namespace GA.Business.Core.Config;

using Atonal;
using Business.Config;
using Extensions;
using Notes;
using System.Collections.Generic;

public class ModesConfigCache
{
    private readonly ConcurrentDictionary<string, ModeCacheValue> _modeByName = new();
    private Guid _lastKnownVersion = Guid.Empty;

    public static ModesConfigCache Instance { get; } = new();

    private ModesConfigCache()
    {
        InvalidateIfNeeded();
    }

    public void InvalidateIfNeeded()
    {
        var currentVersion = ModesConfig.GetVersion();
        if (currentVersion == _lastKnownVersion) return;

        _modeByName.Clear();
        LoadModes();
        _lastKnownVersion = currentVersion;
    }

    private void LoadModes()
    {
        try
        {
            // Try to load from config file
            var allModes = ModesConfig.GetAllModes().ToImmutableList();
            foreach (var mode in allModes)
            {
                _modeByName[mode.Name] = ModeCacheValue.CreateFromMode(mode);
            }
        }
        catch (Exception)
        {
            // If config file loading fails, use default modes
            LoadDefaultModes();
        }
    }

    private void LoadDefaultModes()
    {
        // Major scale modes
        AddDefaultMode("Ionian", "<2 5 4 3 6 1>", "C D E F G A B", "The standard major scale (1st mode of the major scale).");
        AddDefaultMode("Dorian", "<2 5 4 3 6 1>", "C D Eb F G A Bb", "A minor mode with a raised 6th degree (2nd mode of the major scale).");
        AddDefaultMode("Phrygian", "<2 5 4 3 6 1>", "C Db Eb F G Ab Bb", "A minor mode with a lowered 2nd degree (3rd mode of the major scale).");
        AddDefaultMode("Lydian", "<2 5 4 3 6 1>", "C D E F# G A B", "A major mode with a raised 4th degree (4th mode of the major scale).");
        AddDefaultMode("Mixolydian", "<2 5 4 3 6 1>", "C D E F G A Bb", "A major mode with a lowered 7th degree (5th mode of the major scale).");
        AddDefaultMode("Aeolian", "<2 5 4 3 6 1>", "C D Eb F G Ab Bb", "The natural minor scale (6th mode of the major scale).");
        AddDefaultMode("Locrian", "<2 5 4 3 6 1>", "C Db Eb F Gb Ab Bb", "A diminished mode with a lowered 2nd and 5th degree (7th mode of the major scale).");

        // Harmonic minor modes
        AddDefaultMode("HarmonicMinor", "<2 4 5 4 5 2>", "C D Eb F G Ab B", "Minor scale with a raised 7th degree.");

        // Melodic minor modes
        AddDefaultMode("MelodicMinor", "<2 5 4 4 5 1>", "C D Eb F G A B", "The ascending melodic minor scale.");

        // Pentatonic scales
        AddDefaultMode("MajorPentatonic", "<3 2 0 4 1>", "C D E G A", "The first mode of the pentatonic scale.");
        AddDefaultMode("MinorPentatonic", "<3 2 0 4 1>", "C Eb F G Bb", "The third mode of the pentatonic scale, commonly used in blues music.");
    }

    private void AddDefaultMode(string name, string intervalClassVector, string notes, string description)
    {
        // Create F# option types for description and alternateNames
        var descriptionOption = Microsoft.FSharp.Core.FSharpOption<string>.Some(description);
        var alternateNamesOption = Microsoft.FSharp.Core.FSharpOption<IReadOnlyList<string>>.None;

        // Create the ModeInfo using the constructor
        var mode = new ModesConfig.ModeInfo(name, intervalClassVector, notes, descriptionOption, alternateNamesOption);

        _modeByName[name] = ModeCacheValue.CreateFromMode(mode);
    }

    public IEnumerable<ModeCacheValue> GetAllModes()
    {
        InvalidateIfNeeded();

        return _modeByName.Values;
    }

    public ModeCacheValue? TryGetMode(string name)
    {
        InvalidateIfNeeded();

        _modeByName.TryGetValue(name, out var mode);
        return mode;
    }

    public IReadOnlyList<string> ListAllModeNames()
    {
        InvalidateIfNeeded();

        return [.._modeByName.Keys];
    }

    public IReadOnlyList<ModeCacheValue> FindModesByName(string searchTerm)
    {
        InvalidateIfNeeded();

        return _modeByName.Values
            .Where(mode => mode.Mode.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToImmutableList();
    }

    public sealed record ModeCacheValue(ModesConfig.ModeInfo Mode, PitchClassSet PitchClassSet)
    {
        public static ModeCacheValue CreateFromMode(ModesConfig.ModeInfo mode)
        {
            if (!AccidentedNoteCollection.TryParse(mode.Notes, null, out var notes)) throw new InvalidOperationException($"Failed parsing notes: {mode.Notes}");
            return new(mode, notes.ToPitchClassSet());
        }
    }
}