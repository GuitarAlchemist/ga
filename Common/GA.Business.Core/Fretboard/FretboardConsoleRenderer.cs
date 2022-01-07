using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Intervals;

namespace GA.Business.Core.Fretboard;

public class FretboardConsoleRenderer
{
    public static void Render(
        Fretboard fretboard,
        Options? aOptions= null)
    {
        static void MarkerColor() => Console.ForegroundColor = ConsoleColor.Cyan;
        static void FretColor() => Console.ForegroundColor = ConsoleColor.DarkGray;
        static void FrettedPosition() => Console.ForegroundColor = ConsoleColor.White;
        static void OpenPitchColor() => Console.ForegroundColor = ConsoleColor.Blue;
        static string Pad(string s, int padTotalLength = 4) => s.PadRight(padTotalLength);
        
        void RenderPositionsAndFrets()
        {
            var options = aOptions ?? Options.Default;
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
                            var pitch = open.Pitch;
                            var sPitch = Pad($"({pitch})");
                            OpenPitchColor();
                            Console.Write($"{sPitch} ║");
                        },
                        fretted =>
                        {
                            FrettedPosition();
                            var pitch = fretted.Pitch;
                            Console.Write(Pad($"{pitch}"));

                            FretColor();
                            if (openPitches.Contains(pitch)) OpenPitchColor();
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