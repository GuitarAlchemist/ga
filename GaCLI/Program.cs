using System.Collections.Specialized;
using GA.Business.Core.Atonal;
using GA.Core.Combinatorics;

foreach (var variation in PitchClassVariations.SharedInstance)
{
    Console.WriteLine(variation.ToString());
}

// --------

/*
var a = Note.SharpKey.Items.ToNormedCartesianProduct<Note.SharpKey, IntervalClass>();
var cc = new IntervalClassVector(a.ByNormCounts());

var sb = new StringBuilder();
foreach (var pair in a)
{
    sb.AppendLine(pair.ToString());
}

var s = sb.ToString();
Console.WriteLine(s);
Console.WriteLine();
Console.WriteLine("Counts:");
foreach (var (ic, count) in a.ByNormCounts())
{
    Console.WriteLine($"{ic}: {count}");
}

*/