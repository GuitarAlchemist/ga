namespace GA.Business.Core.Fretboard.Config;

using System.Collections.Immutable;
using Primitives;
using Notes;
using static Notes.Pitch.Sharp;

/// <summary>
/// Guitar tuning
/// </summary>
/// <remarks>
/// References:
/// https://www.guitarworld.com/lessons/11-alternate-tunings-every-guitarist-should-know
/// https://www.stringsbymail.com/TuningChart.pdf
/// </remarks>
[PublicAPI]
public class Tuning
{
    private readonly IReadOnlyDictionary<Str, Pitch> _pitchByStr;

    public static Tuning Default => Guitar.Standard;

    /// <summary>
    /// https://en.wikipedia.org/wiki/Guitar_tunings
    /// </summary>
    public static class Guitar
    {
        public const int DefaultFretCount = 22;

        public static Tuning Standard => CreateTuning("Standard", E2, A2, D3, G3, B3, E4);
        public static Tuning DropD => CreateTuning("Drop D", D2, A2, D3, G3, B3, E4);
        public static Tuning DoubleDropD => CreateTuning("Double drop D", D2, A2, D3, G3, B3, D4);
        public static Tuning Dadgad => CreateTuning("DAGGAD", D2, A2, D3, G3, A3, D4);
        public static Tuning OpenD => CreateTuning("Open D", D2, A2, D3, FSharp3, A3, D4);

        private static Tuning CreateTuning(
            string tuningName, 
            params Pitch[] pitches) =>
            new(
                new(
                    nameof(Guitar), 
                    tuningName,
                    22)
                , pitches
            );
    }

    /// <summary>
    /// https://en.wikipedia.org/wiki/Bass_guitar_tuning
    /// </summary>
    public static class Bass
    {
        public const int DefaultFretCount = 22;

        public static Tuning Standard => CreateTuning("Standard", E1, A1, D2, G2);
        public static Tuning Tenor => CreateTuning("Tenor", A1, D2, G2, C3);

        [PublicAPI]
        public static class FiveString
        {
            public static Tuning Standard => CreateTuning("Standard", B0, E1, A1, D2, G2);
            public static Tuning Tenor => CreateTuning("Tenor", E1, A1, D2, G2, C3);
        }

        [PublicAPI]
        public static class SixString
        {
            public const int DefaultFretCount = 22;

            public static Tuning Standard => CreateTuning("Standard", B0, E1, A1, D2, G2, C3);
        }

        private static Tuning CreateTuning(string tuningName, params Pitch[] pitches) => new(new(nameof(Bass), tuningName), pitches);
    }

    public static class Ukulele
    {
        public const int DefaultFretCount = 15;

        public static Tuning Standard => CreateTuning("Standard", E2, A2, D3, G3, B3, E4);

        private static Tuning CreateTuning(string tuningName, params Pitch[] pitches) => new(new(nameof(Ukulele), tuningName), pitches);
    }

    public static class Banjo
    {
        public const int DefaultFretCount = 22;

        public static Tuning Cello => CreateTuning("Cello", E2, A2, D3, G3, B3, E4);

        private static Tuning CreateTuning(string tuningName, params Pitch[] pitches) => new(new(nameof(Banjo), tuningName), pitches);
    }

    public static class Mandolin
    {
        public const int DefaultFretCount = 17;

        public static Tuning Standard => CreateTuning("Standard", G3, D4, A4, E5);

        private static Tuning CreateTuning(string tuningName, params Pitch[] pitches) => new(new(nameof(Mandolin), tuningName), pitches);
    }

    public static class Balalaika
    {
        public const int DefaultFretCount = 16;

        public static Tuning Alto => CreateTuning("Alto", E3, E3, A3);
        public static Tuning Bass => CreateTuning("Bass", E2, A2, D3);
        public static Tuning Contrabass => CreateTuning("Contrabass", E1, A1, D2);
        public static Tuning Piccolo => CreateTuning("Piccolo", B4, E5, A5);
        public static Tuning Prima => CreateTuning("Prima", E4, E4, A4);
        public static Tuning Sekunda => CreateTuning("Sekunda", A3, A3, D4);

        private static Tuning CreateTuning(string tuningName, params Pitch[] pitches) => new(new(nameof(Balalaika), tuningName), pitches);
    }

    internal Tuning(
        TuningInfo tuningInfo,
        params Pitch[] pitches) 
            : this(tuningInfo, new PitchCollection(pitches))
    {
    }

    internal Tuning(
        TuningInfo tuningInfo,
        PitchCollection pitches)
    {
        TuningInfo = tuningInfo;
        Pitches = pitches ?? throw new ArgumentNullException(nameof(pitches));
        _pitchByStr = PitchByStr(pitches);
    }

    public TuningInfo TuningInfo { get; }
    public PitchCollection Pitches { get; }
    public Pitch this[Str str] => _pitchByStr[str];

    private static IReadOnlyDictionary<Str, Pitch> PitchByStr(IEnumerable<Pitch> pitches)
    {
        var pitchesList = pitches.ToImmutableList();
        var lowestPitchFirst = pitchesList.First() < pitchesList.Last();
        if (lowestPitchFirst) pitchesList = pitchesList.Reverse();

        var str = Str.Min;
        var dict = new Dictionary<Str, Pitch>();
        foreach (var pitch in pitchesList)
        {
            dict[str++] = pitch;
        }

        // Result
        var result = dict.ToImmutableDictionary();

        return result;
    }

    public override string ToString() => TuningInfo.ToString();
}