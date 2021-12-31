using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class FretboardConsoleRenderer
{
    public static void Render(Fretboard fretboard)
    {
        static void MerkerColor() => Console.ForegroundColor = ConsoleColor.Cyan;
        static void FretColor() => Console.ForegroundColor = ConsoleColor.DarkGray;
        static void FrettedPosition() => Console.ForegroundColor = ConsoleColor.White;
        static void OpenPitchColor() => Console.ForegroundColor = ConsoleColor.Blue;
        static string Pad(string s, int padTotalLength = 4) => s.PadRight(padTotalLength);

        var positionsByStr = fretboard.Positions.ToLookup(position => position.Str);
        var openPitches = fretboard.OpenPositions.Select(open => open.Pitch).ToImmutableHashSet();
        foreach (var str in fretboard.Strings)
        {
            MerkerColor();
            Console.Write($"Str {str}: ");
            Console.ResetColor();
            var stringPositions = positionsByStr[str];
            foreach (var position in stringPositions)
            {
                position.Switch(
                    _ =>
                    {
                        FretColor();
                        Console.Write("X ║");
                    },
                    open =>
                    {
                        OpenPitchColor();
                        Console.Write(Pad($"{open.Pitch}"));
                    },
                    fretted =>
                    {
                        FretColor();
                        if (openPitches.Contains(fretted.Pitch)) OpenPitchColor();
                        Console.Write("| ");

                        FrettedPosition();
                        Console.Write(Pad($"{fretted.Pitch}"));
                    });
                Console.Write(" ");
            }
            Console.WriteLine();
        }

        var fretMarkers = GetFretMarkers();
        Console.Write("     ");
        foreach (var fret in fretboard.Frets)
        {
            var isFretMarked = fretMarkers.Contains(fret);
            var marker = "";
            if (isFretMarked)
            {
                marker = fret % 12 == 0 ? "**" : "*";
            }

            MerkerColor();
            Console.Write(Pad(Pad(marker)));
            Console.Write("   ");
            Console.ResetColor();
        }

        static IReadOnlySet<Fret> GetFretMarkers() => new HashSet<Fret>(new Fret[] {3, 5, 7, 9, 12, 15, 17, 19, 21, 24});
    }
    
}