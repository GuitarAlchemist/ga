namespace GA.Business.Core.Config;

using Atonal;
using Business.Config;
using Extensions;
using Notes;

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
        var allModes = ModesConfig.GetAllModes().ToImmutableList();
        foreach (var mode in allModes)
        {
            _modeByName[mode.Name] = ModeCacheValue.CreateFromMode(mode);
        }
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