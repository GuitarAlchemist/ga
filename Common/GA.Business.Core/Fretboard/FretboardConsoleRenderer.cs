namespace GA.Business.Core.Fretboard;

using Primitives;
using GA.Core;

public class FretboardConsoleRenderer
{
    public static void Render(
        Fretboard fretboard,
        Options? aOptions= null)
    {
        static void MarkerColor() => Console.ForegroundColor = ConsoleColor.Cyan;
        static void FretColor() => Console.ForegroundColor = ConsoleColor.DarkGray;
        static void FrettedPositionColor() => Console.ForegroundColor = ConsoleColor.White;
        static void OpenPositionColor() => Console.ForegroundColor = ConsoleColor.Blue;
        static void CapoColor() => Console.ForegroundColor = ConsoleColor.DarkCyan;
        static void NotAvailablePositionColor() => Console.ForegroundColor = ConsoleColor.DarkGray;
        static string Pad(string s, int padTotalLength = 4) => s.PadRight(padTotalLength);
        
        void RenderPositionsAndFrets()
        {
            var options = aOptions ?? Options.Default; // TODO
            var positionsByStr = fretboard.Positions.ToLookup(position => position.Str);
            var openPitches =
                fretboard.CapoFret.HasValue
                    ? fretboard[fretboard.CapoFret.Value].Select(fretted => fretted.Pitch).ToImmutableHashSet()
                    : fretboard.OpenPositions.Select(open => open.Pitch).ToImmutableHashSet();
            foreach (var str in fretboard.Strings)
            {
                MarkerColor();
                Console.Write($"Str {str}:");
                Console.ResetColor();

                // String positions
                var stringPositions = positionsByStr[str];
                foreach (var position in stringPositions)
                {
                    position.Switch(
                        _ => { }, // Don't render
                        open =>
                        {
                            // Open position
                            var sOpen =  Pad($"({open.Pitch})");
                            if (fretboard.CapoFret.HasValue) 
                                NotAvailablePositionColor();
                            else 
                                OpenPositionColor();
                            Console.Write($"{sOpen}");

                            // Nut
                            OpenPositionColor();
                            Console.Write(" ║");
                        },
                        fretted =>
                        {
                            // Fretted position
                            var (_, fret, pitch) = fretted;
                            if (fretboard.CapoFret.TryGetValue(out var capoFret) && fret < capoFret)
                                NotAvailablePositionColor();
                            else
                                FrettedPositionColor();
                            Console.Write(Pad($"{pitch}"));

                            // Fret
                            FretColor();
                            if (openPitches.Contains(pitch)) OpenPositionColor();
                            Console.Write("|");
                        });
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