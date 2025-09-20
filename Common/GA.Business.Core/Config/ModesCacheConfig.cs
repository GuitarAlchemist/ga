﻿namespace GA.Business.Core.Config;

using Atonal;
using Business.Config;
using Extensions;
using Notes;

public class ModesConfigCache
{
    private ImmutableDictionary<string, ModeCacheValue> _modeByName = ImmutableDictionary<string, ModeCacheValue>.Empty;

    private ImmutableDictionary<int, ModeCacheValue> _modeByPitchClassSetIdValue =
        ImmutableDictionary<int, ModeCacheValue>.Empty;

    private Guid _lastKnownVersion = Guid.Empty;
    private ILookup<IntervalClassVector, ModeCacheValue>? _modesByIntervalClassVector;

    public static ModesConfigCache Instance { get; } = new();

    private void ReloadIfNeeded()
    {
        var currentVersion = ModesConfig.GetVersion();
        if (currentVersion == _lastKnownVersion) return;

        ReloadModes();
        _lastKnownVersion = currentVersion;
    }

    private void ReloadModes()
    {
        var modes = ModesConfig.GetAllModes().Select(ModeCacheValue.CreateFromMode).ToImmutableList();
        _modeByName = modes.ToImmutableDictionary(value => value.Mode.Name);
        _modeByPitchClassSetIdValue = modes.ToImmutableDictionary(value => value.PitchClassSet.Id.Value);
        _modesByIntervalClassVector =
            modes.ToLookup(value => IntervalClassVector.Parse(value.Mode.IntervalClassVector));

        var sb = new StringBuilder();
        foreach (var intervalVector in ModalFamily.ModalIntervalVectors)
        {
            if (!ModalFamily.TryGetValue(intervalVector, out var modalFamily))
                throw new InvalidOperationException($"Modal family not found for interval vector: {intervalVector}");
            sb.AppendLine();
            sb.AppendLine($"# {intervalVector}");
            foreach (var pcs in modalFamily.Modes)
            {
                var sNotes = string.Join(" ", pcs.Notes.Select(chromatic => chromatic.ToSharp().ToString()));
                var video = pcs.ScaleVideoUrl;
                if (video == null) continue;
                sb.AppendLine($"""
                               {pcs.Id.Value}:
                                 Url: {pcs.ScalePageUrl}
                                 Video: {video}
                                 Notes: "{sNotes}"
                                 AlternateNames: []
                               """);
            }
        }

        var s = sb.ToString();
    }

    public IEnumerable<ModeCacheValue> GetAllModes()
    {
        ReloadIfNeeded();

        return _modeByName.Values;
    }

    public bool TryGetModeByName(string name, out ModeCacheValue mode)
    {
        ReloadIfNeeded();

        mode = null!;
        if (!_modeByName.TryGetValue(name, out var foundMode)) return false;
        mode = foundMode;
        return true;
    }

    public bool TryGetModeByPitchClassSetId(int pitchClassSetId, out ModeCacheValue mode)
    {
        ReloadIfNeeded();

        mode = null!;
        if (!_modeByPitchClassSetIdValue.TryGetValue(pitchClassSetId, out var foundMode)) return false;
        mode = foundMode;
        return true;
    }

    public sealed record ModeCacheValue(
        ModesConfig.ModeInfo Mode,
        PitchClassSet PitchClassSet)
    {
        public static ModeCacheValue CreateFromMode(ModesConfig.ModeInfo mode)
        {
            if (!AccidentedNoteCollection.TryParse(mode.Notes, null, out var notes))
                throw new InvalidOperationException($"Failed parsing notes: {mode.Notes}");
            return new(mode, notes.ToPitchClassSet());
        }
    }
}