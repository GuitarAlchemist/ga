using GA.Business.Core.Fretboard;
using GA.Business.Core.Intervals;
using GA.Business.Core.Notes;
using GA.Business.Core.Tonal.Modes;

var notes = MajorScaleMode.Dorian.Notes;

var intervals = new List<(Note, Note,Interval.Simple)>();
for (var i = 0; i < notes.Count; i++)
{
    var note1 = notes.ElementAt(i);
    var i2 = (i + 6) % notes.Count;
    var note2 =  notes.ElementAt(i2);
    var interval = note1.GetInterval(note2);
    intervals.Add((note1, note2, interval));
}

var sIntervals = intervals.ToString();

/*

Ideas: 
Decompose horizontal movement into m3/M3 fret intervals - See https://www.youtube.com/watch?v=Ab3nqlbl9us

*/

// RenderFretboard();

void GetMajorModes()
{
    var majorModes = MajorScaleMode.All;
    foreach (var mode in majorModes)
    {
        Console.WriteLine(mode.Name);
        Console.WriteLine("Notes:          " + mode.Notes);
        Console.WriteLine("Intervals:      " + mode.Intervals);
        Console.WriteLine("Formula:        " + mode.Formula.ToString().Trim());
        Console.WriteLine("ScaleNumber:    " + mode.Identity);
        Console.WriteLine("ScalePageUrl: " + $"{mode.Identity.ScalePageUrl}");
        Console.WriteLine("ScaleVideoUrl: " + $"{mode.Identity.ScaleVideoUrl}");
        Console.WriteLine();
    }
}

void RenderFretboard()
{
    var fretBoard = Fretboard.Guitar();
    var aa = fretBoard.OpenPositions;
    var bb = fretBoard.Positions;
    Console.WriteLine($"Tuning: {fretBoard.Tuning}");
    Console.WriteLine();
    FretboardConsoleRenderer.Render(fretBoard);
}

