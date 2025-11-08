namespace GaCLI;

public class SomeStuff
{
    private static void RenderGuitarFretboard()
    {
        Console.WriteLine(Fretboard.Default);
    }

    // TODO: Fix to use new YamlDotNet-based API instead of old type provider
    /*
    static void RenderUkuleleFretboard()
    {
        if (!PitchCollection.TryParse(InstrumentsConfig.Instruments.Ukulele.Baritone.Tuning, null, out var result)) throw new PitchCollectionParseException();
        var tuning = new Tuning(result);
        var fretBoard = new Fretboard(tuning, 15);
        Console.WriteLine(fretBoard.ToString());
    }
    */
}
