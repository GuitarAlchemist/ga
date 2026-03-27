namespace GaApi.Services;

using System.Globalization;
using System.Text;

/// <summary>
///     Converts timestamped guitar positions into standard 6-string ASCII tablature
/// </summary>
public class PositionToTabGenerator(ILogger<PositionToTabGenerator> logger)
{
    private const double TimeProximityToleranceMs = 200.0;
    private const int EventsPerMeasure = 4;
    private const int StringCount = 6;

    // Standard tuning string names, high to low
    private static readonly string[] StringNames = ["e", "B", "G", "D", "A", "E"];

    /// <summary>
    ///     Generate ASCII tab from timestamped guitar positions
    /// </summary>
    public string GenerateTab(IReadOnlyList<TimestampedGuitarPosition> positions, string? sourceUrl = null)
    {
        if (positions.Count == 0)
        {
            return "No notes detected";
        }

        // Group positions by time proximity
        var chordEvents = GroupByTimeProximity(positions);
        logger.LogInformation("Grouped {PositionCount} positions into {EventCount} chord events",
            positions.Count, chordEvents.Count);

        var sb = new StringBuilder();

        // Header
        sb.AppendLine(sourceUrl is not null
            ? $"# Generated from {sourceUrl} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
            : $"# Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"# {chordEvents.Count} events detected");
        sb.AppendLine();

        // Break into measures and render
        var measures = ChunkIntoMeasures(chordEvents);

        foreach (var measure in measures)
        {
            RenderMeasure(sb, measure);
        }

        return sb.ToString();
    }

    private List<ChordEvent> GroupByTimeProximity(IReadOnlyList<TimestampedGuitarPosition> positions)
    {
        var sorted = positions.OrderBy(p => p.TimestampSeconds).ToList();
        var events = new List<ChordEvent>();
        var currentGroup = new List<TimestampedGuitarPosition> { sorted[0] };

        for (var i = 1; i < sorted.Count; i++)
        {
            var timeDiffMs = (sorted[i].TimestampSeconds - currentGroup[^1].TimestampSeconds) * 1000.0;

            if (timeDiffMs <= TimeProximityToleranceMs)
            {
                currentGroup.Add(sorted[i]);
            }
            else
            {
                events.Add(MergeToChordEvent(currentGroup));
                currentGroup = [sorted[i]];
            }
        }

        if (currentGroup.Count > 0)
        {
            events.Add(MergeToChordEvent(currentGroup));
        }

        return events;
    }

    private static ChordEvent MergeToChordEvent(List<TimestampedGuitarPosition> group)
    {
        var timestamp = group[0].TimestampSeconds;
        // Merge all positions, taking the highest-confidence position per string
        var positionsByString = new Dictionary<int, (int Fret, double Confidence)>();

        foreach (var tsp in group)
        {
            foreach (var pos in tsp.Positions)
            {
                if (!positionsByString.TryGetValue(pos.String, out var existing) ||
                    pos.Confidence > existing.Confidence)
                {
                    positionsByString[pos.String] = (pos.Fret, pos.Confidence);
                }
            }
        }

        return new ChordEvent(timestamp, positionsByString);
    }

    private static List<List<ChordEvent>> ChunkIntoMeasures(List<ChordEvent> events)
    {
        var measures = new List<List<ChordEvent>>();

        for (var i = 0; i < events.Count; i += EventsPerMeasure)
        {
            var chunk = events.GetRange(i, Math.Min(EventsPerMeasure, events.Count - i));
            measures.Add(chunk);
        }

        return measures;
    }

    private static void RenderMeasure(StringBuilder sb, List<ChordEvent> events)
    {
        // Build each string line for this measure
        // String numbers: 1 = high e, 2 = B, 3 = G, 4 = D, 5 = A, 6 = low E
        var lines = new string[StringCount];

        for (var s = 0; s < StringCount; s++)
        {
            var stringNumber = s + 1; // 1-indexed
            var lineSb = new StringBuilder();
            lineSb.Append(StringNames[s]);
            lineSb.Append('|');

            foreach (var evt in events)
            {
                lineSb.Append("---");
                if (evt.PositionsByString.TryGetValue(stringNumber, out var pos))
                {
                    var fretStr = pos.Fret.ToString(CultureInfo.InvariantCulture);
                    lineSb.Append(fretStr);
                    // Pad shorter fret numbers
                    if (fretStr.Length == 1)
                    {
                        // single digit: no extra padding needed in basic format
                    }
                }
                else
                {
                    lineSb.Append('-');
                }
            }

            lineSb.Append("---|");
            lines[s] = lineSb.ToString();
        }

        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
    }

    private record ChordEvent(double TimestampSeconds, Dictionary<int, (int Fret, double Confidence)> PositionsByString);
}
