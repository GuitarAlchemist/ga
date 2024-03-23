namespace GA.Business.Core.Fretboard;

using Primitives;

public class FretboardConsoleRenderer
{
    public static void Render(
        Fretboard fretboard,
        Options? aOptions= null)
    {
        static void MarkerColor() => Console.ForegroundColor = ConsoleColor.Cyan;
        static void FretColor() => Console.ForegroundColor = ConsoleColor.DarkGray;
        static void FingeredPositionColor() => Console.ForegroundColor = ConsoleColor.White;
        static void OpenPositionColor() => Console.ForegroundColor = ConsoleColor.Blue;
        // static void CapoColor() => Console.ForegroundColor = ConsoleColor.DarkCyan;
        static void NotAvailablePositionColor() => Console.ForegroundColor = ConsoleColor.DarkGray;
        static string Pad(string s, int padTotalLength = 5) => s.PadRight(padTotalLength);
        
        void RenderPositionsAndFrets()
        {
            var options = aOptions ?? Options.Default; // TODO
            var playedPositions = fretboard.Positions.Played;
            var openFret = fretboard.Capo ?? Fret.Open;
            var openMidiNotes =
                fretboard.Positions.Played[openFret]
                    .Select(fretted => fretted.MidiNote)
                    .ToImmutableHashSet();
            foreach (var str in fretboard.Strings)
            {
                MarkerColor();
                Console.Write($"Str {str}:");
                Console.ResetColor();

                // String positions
                foreach (var playedPosition in playedPositions[str])
                {
                    var (location, midiNote) = playedPosition;

                    if (location.Fret == Fret.Open)
                    {
                        // Open position
                        var sOpen =  Pad($"({playedPosition.MidiNote.ToSharpPitch()})");
                        if (fretboard.Capo.HasValue) 
                            NotAvailablePositionColor();
                        else 
                            OpenPositionColor();
                        Console.Write($"{sOpen}");

                        // Nut
                        OpenPositionColor();
                        Console.Write(" ║");
                    }
                    else
                    {
                        // Fingered positions
                        if (fretboard.Capo.TryGetValue(out var capoFret) && location.Fret < capoFret)
                            NotAvailablePositionColor();
                        else
                            FingeredPositionColor();
                        Console.Write(Pad($"{midiNote}"));

                        // Fret
                        FretColor();
                        if (openMidiNotes.Contains(midiNote)) OpenPositionColor(); // Same as open pitch
                        Console.Write("|");
                    }
                    Console.Write(" ");
                }

                Console.WriteLine();
            }
        }

        static void RenderFretMarkers(
            IEnumerable<Fret> frets,
            Func<Fret, string> markerTextCallback,
            int? offset = 0)
        {
            if (offset.HasValue) Console.Write(new string(' ', offset.Value));
            var fretMarkers = new HashSet<Fret>(new Fret[] { 3, 5, 7, 9, 12, 15, 17, 19, 21, 24 });
            foreach (var fret in frets)
            {
                var marker = fretMarkers.Contains(fret) ? markerTextCallback(fret) : "";
                MarkerColor();
                Console.Write(Pad(Pad(marker)));
                Console.Write(new string(' ', 2));
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        // Render
        RenderPositionsAndFrets();
        RenderFretMarkers(fretboard.Frets, fret => fret % 12 == 0 ? "**" : "*", 9);
        RenderFretMarkers(fretboard.Frets, fret => fret.Value.ToString(), 12);
    }

    public readonly record struct Options(bool LeftHanded)
    {
        public static readonly Options Default = new(LeftHanded:false); // TODO: Add support for left handed
    }
}