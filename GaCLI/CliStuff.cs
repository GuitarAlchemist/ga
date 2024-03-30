
namespace GaCLI;

using GA.Business.Config;
using GA.Business.Core.Notes;

public class SomeStuff
{
    static void RenderGuitarFretboard()
    {
        Console.WriteLine(Fretboard.Default);
    }

    static void RenderUkuleleFretboard()
    {
        if (!PitchCollection.TryParse(Instruments.Instrument.Ukulele.Baritone.Tuning, null, out var result)) throw new PitchCollectionParseException();
        var tuning = new Tuning(result);
        var fretBoard = new Fretboard(tuning, 15);
        Console.WriteLine(fretBoard.ToString());
    }
}