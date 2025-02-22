﻿namespace GA.Business.Core.Config
{
    using Atonal;
    using Extensions;
    using GA.Business.Config;
    using Notes;
    using ScaleInfo = Business.Config.ScalesConfig.ScaleInfo;

    public class ScalesConfigCache
    {
        private readonly ConcurrentDictionary<string, ScaleCacheValue> _scalesByName = new();
        private Guid _lastKnownVersion = Guid.Empty;

        public static ScalesConfigCache Instance { get; } = new();

        private ScalesConfigCache()
        {
            InvalidateIfNeeded();
        }

        public void InvalidateIfNeeded()
        {
            var currentVersion = ScalesConfig.GetVersion();
            if (currentVersion == _lastKnownVersion) return;
            
            _scalesByName.Clear();
            LoadScales();
            _lastKnownVersion = currentVersion;
        }

        private void LoadScales()
        {
            var allScales = ScalesConfig.GetAllScales().ToImmutableList();
            foreach (var scale in allScales)
            {
                _scalesByName[scale.Name] = ScaleCacheValue.CreateFromScale(scale);
            }
        }

        public IEnumerable<ScaleCacheValue> GetAllScales()
        {
            InvalidateIfNeeded();
            
            return _scalesByName.Values;
        }

        public ScaleCacheValue? TryGetScale(string name)
        {
            InvalidateIfNeeded();
            
            _scalesByName.TryGetValue(name, out var scale);
            return scale;
        }

        public IReadOnlyList<string> ListAllScaleNames()
        {
            InvalidateIfNeeded();

            return [.._scalesByName.Keys];
        }

        public IReadOnlyList<ScaleCacheValue> FindScalesByName(string searchTerm)
        {
            InvalidateIfNeeded();
            
            return _scalesByName.Values
                .Where(scale => scale.Scale.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToImmutableList();
        }

        public sealed record ScaleCacheValue(ScaleInfo Scale, PitchClassSet PitchClassSet)
        {
            public static ScaleCacheValue CreateFromScale(ScaleInfo scale)
            {
                if (!AccidentedNoteCollection.TryParse(scale.Notes, null, out var notes)) throw new InvalidOperationException($"Failed parsing notes: {scale.Notes}");
                return new(scale, notes.ToPitchClassSet());
            }
        }
    }
}