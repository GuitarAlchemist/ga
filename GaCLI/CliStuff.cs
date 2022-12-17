
namespace GaCLI;

using GA.Business.Config;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Notes;
using GA.Business.Core.Scales;
using GA.Business.Core.Tonal.Modes;

public class SomeStuff
{
    static void RenderGuitarFretboard()
    {
        Console.WriteLine(Fretboard.Default);
    }

    static void RenderUkuleleFretboard()
    {
        var tuning = new Tuning(PitchCollection.Parse(Instruments.Instrument.Ukulele.Baritone.Tuning));
        var fretBoard = new Fretboard(tuning, 15);
        Console.WriteLine(fretBoard.ToString());
    }
}