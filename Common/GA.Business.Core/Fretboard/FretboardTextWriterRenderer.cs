namespace GA.Business.Core.Fretboard;

using Primitives;

public class FretboardTextWriterRenderer
{
    public static void Render(Fretboard fretboard, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        ArgumentNullException.ThrowIfNull(fretboard);

        RenderPositionsAndFrets();
        RenderFretMarkers(fretboard.Frets, fret => fret % 12 == 0 ? "**" : "*", 9);
        RenderFretMarkers(fretboard.Frets, fret => fret.Value.ToString(), 12);
        textWriter.Flush();
        return;

        static string Pad(string s, int padTotalLength = 5) => s.PadRight(padTotalLength);

        void RenderPositionsAndFrets()
        {
            var playedPositions = fretboard.Positions.Played;
            foreach (var str in fretboard.Strings)
            {
                textWriter.Write($"Str {str}:");
                RenderStringPositions(str, playedPositions[str]);
                textWriter.WriteLine();
            }
        }

        void RenderStringPositions(Str str, IEnumerable<Position.Played> playedPositions)
        {
            foreach (var playedPosition in playedPositions)
            {
                var (location, pitch) = playedPosition;
                if (location.Fret == Fret.Open)
                {
                    textWriter.Write($"{Pad($"({playedPosition.MidiNote.ToSharpPitch()})")}");
                    textWriter.Write(" ║");
                }
                else
                {
                    if (fretboard.Capo.TryGetValue(out var capoFret) && location.Fret < capoFret) textWriter.Write(Pad($"{pitch}"));
                    textWriter.Write('|');
                }

                textWriter.Write(' ');
            }
        }

        void RenderFretMarkers(
            IEnumerable<Fret> frets,
            Func<Fret, string> markerTextCallback,
            int? offset = 0)
        {
            if (offset.HasValue) textWriter.Write(new string(' ', offset.Value));
            var fretMarkers = new HashSet<Fret>(new Fret[] { 3, 5, 7, 9, 12, 15, 17, 19, 21, 24 });
            foreach (var fret in frets)
            {
                var marker = fretMarkers.Contains(fret) ? markerTextCallback(fret) : "";
                textWriter.Write(Pad(Pad(marker)));
                textWriter.Write(new string(' ', 2));
            }
            textWriter.WriteLine();
        }
    }

    public readonly record struct Options(bool LeftHanded)
    {
        public static readonly Options Default = new(LeftHanded:false); // TODO: Add support for left handed
    }
}