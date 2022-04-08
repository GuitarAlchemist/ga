using System.Collections.Immutable;
using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Intervals;
using GA.Business.Core.Notes;
using GA.Business.Core.Scales;
using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes;

var keyOfB = Key.Major.B;
var keyOfBNotes = keyOfB.GetNotes();

var allValid = PitchClassSetIdentity.GetAllValid().ToImmutableList();

var allValidSevenNotes = allValid.Where(number => number.Notes.Count == 7);

var aaaa = allValid.Where(number => number.IntervalVector == 254361).ToImmutableList();

var lookup = allValidSevenNotes.ToLookup(number => number.IntervalVector);
var vectors =
    lookup
        .Where(grouping => grouping.Count() > 1)
        .OrderByDescending(numbers => numbers.Count())
        .Select(numbers => numbers.Key)
        .Distinct()
        .ToImmutableList();


foreach (var vector in vectors)
{
    var group = lookup[vector];
    foreach (var num in group)
    {
        var name = ScaleNameByIdentity.Get(num);
    }
}   


var majorModes = MajorScaleMode.All;
var harmonicMinorModes = HarmonicMinorMode.All;
var melodicMinorModes = MelodicMinorMode.All;

foreach (var mode in majorModes)
{
    Console.WriteLine(mode.Name);
    Console.WriteLine("Notes:          " + mode.Notes);
    Console.WriteLine("Intervals:      " + mode.Intervals);
    Console.WriteLine("Formula:        " + mode.Formula.ToString().Trim());
    Console.WriteLine("ScaleNumber:    " + mode.ModeIdentity);
    Console.WriteLine("ScalePageUrl: " + $"{mode.ModeIdentity.ScalePageUrl}");
    Console.WriteLine("ScaleVideoUrl: " + $"{mode.ModeIdentity.ScaleVideoUrl}");
    Console.WriteLine();
}

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

var majorPentatonicModes = MajorPentatonicMode.All;

var fretBoard = Fretboard.Guitar();
var aa = fretBoard.OpenPositions;
var bb = fretBoard.Positions;
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard); // m3/M3 - 

