
using GA.Business.Core.Notes;
using PCRE;

namespace GA.Business.Core.Fretboard.Config;

public abstract class TuningParserBase
{
}

/// <summary>
/// See https://www.stringsbymail.com/TuningChart.pdf - © Fretted Friends Music 2008
/// </summary>
public class FrettedFriendsTuningParser : TuningParserBase
{
    public static readonly FrettedFriendsTuningParser Instance = new();

    //language=regexp
    private const string _pitchesRegexPattern = @"\s((?:(?:[A-G])(?:#?)(?:10|11|[0-9])(?:\s|$))*$)$";
    //language=regexp
    private const string _infoRegexPattern = @"^(?'instrument'(?:[^-]*)\s*-\s*(?'tuning'[^-]*)|^(?'instrument'.*))$";
    private static readonly PcreRegex _pitchesRegex = new(_pitchesRegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);
    private static readonly PcreRegex _infoRegex = new(_infoRegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase | PcreOptions.DupNames);

    public bool TryParse(string s, out Tuning parsedTuningCollection)
    {
        parsedTuningCollection = Tuning.Default;
        var match = _pitchesRegex.Match(s);
        if (!match.Success) return false; // Failure

        var pitchesGroup = match.Groups[1];

        // Info
        var infoSegment = s[..pitchesGroup.Index];
        var infoMatch = _infoRegex.Match(infoSegment);
        if (!infoMatch.Success) return false;
        var instrumentName = infoMatch.Groups["instrument"].Value;
        var tuningGroup = infoMatch.Groups["tuning"];
        TuningInfo tuningInfo =
            tuningGroup.IsDefined
                ? new(instrumentName, tuningGroup.Value)
                : new(instrumentName);

        // Pitches
        var pitchesSegment = s[pitchesGroup.Index..];
        if (!PitchCollection.TryParse(pitchesSegment, out var pitchCollection)) return false; // Failure

        // Success
        parsedTuningCollection = new Tuning(tuningInfo, pitchCollection);
        return true;
    }
}

