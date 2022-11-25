using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Notes;
using GA.Core.Combinatorics;

var a = new OrderedCartesianProduct<Note.SharpKey, IntervalClass>();

var b = a.GetTuples();

/*
var normByTuple = new Dictionary<(Note, Note), int>();
foreach (var note1 in Note.AllSharp)
{
    foreach (var note2 in Note.AllSharp)
    {
        var norm = note1.GetNorm(note2);
        var tuple = (note1, note2);
        normByTuple.Add(tuple, norm);
    }
}
var dict1 = normByTuple.ToImmutableDictionary();

*/




foreach (var variation in new VariationsWithRepetitions<ushort>(new ushort[] { 0, 1 }, 12))
{
    Console.WriteLine(variation.ToString());
}