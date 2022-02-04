using GA.Business.Core.Fretboard;
using GA.Business.Core.Tonal;

var ionianMode = Mode.Major.Ionian;
var dorianMode = Mode.Major.Dorian;
var phrygian = Mode.Major.Phrygian;
var lydian = Mode.Major.Lydian;
var mixolydian = Mode.Major.Mixolydian;
var aeolian = Mode.Major.Aeolian;
var locrian = Mode.Major.Locrian;

var harmonicMinor = Mode.HarmonicMinor.HarmonicMinorScale;
var hmm2 = Mode.HarmonicMinor.LocrianNaturalSixth;
var hmm3 = Mode.HarmonicMinor.IonianAugmented;

var melodicMinor = Mode.MelodicMinorMode.MelodicMinor;

var fretBoard = Fretboard.Guitar();
var aa = fretBoard.OpenPositions;
var bb = fretBoard.Positions;
Console.WriteLine($"Tuning: {fretBoard.Tuning}");
Console.WriteLine();
FretboardConsoleRenderer.Render(fretBoard);