using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class FretboardConsoleRenderer
{
    public static void Render(Fretboard fretboard)
    {
        static void MarkerColor() => Console.ForegroundColor = ConsoleColor.Cyan;
        static void FretColor() => Console.ForegroundColor = ConsoleColor.DarkGray;
        static void FrettedPosition() => Console.ForegroundColor = ConsoleColor.White;
        static void OpenPitchColor() => Console.ForegroundColor = ConsoleColor.Blue;
        static string Pad(string s, int padTotalLength = 4) => s.PadRight(padTotalLength);
        
        void RenderPositionsAndFrets()
        {
            var positionsByStr = fretboard.Positions.ToLookup(position => position.Str);
            var openPitches = fretboard.OpenPositions.Select(open => open.Pitch).ToImmutableHashSet();
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
                            OpenPitchColor();
                            var sPitch = Pad($"({open.Pitch})");
                            Console.Write($"{sPitch} ║");
                        },
                        fretted =>
                        {
                            FrettedPosition();
                            Console.Write(Pad($"{fretted.Pitch}"));

                            FretColor();
                            if (openPitches.Contains(fretted.Pitch)) OpenPitchColor();
                            Console.Write("|");
                        });
                    Console.Write(" ");
                }

                Console.WriteLine();
            }
        }

        static void RenderFretMarkers(
            IEnumerable<Fret> frets,
            Func<Fret, string> markerTextCallback)
        {
            Console.Write(new string(' ', 8));
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
        RenderFretMarkers(fretboard.Frets, fret => fret % 12 == 0 ? "**" : "*");
        RenderFretMarkers(fretboard.Frets, fret => fret.Value.ToString());
    }
}