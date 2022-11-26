using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Notes;
using GA.Core.Combinatorics;

foreach (var pair in new NormedCartesianProduct<Note.SharpKey, IntervalClass>().Pairs)
{
    var s = pair.ToString();
    Console.WriteLine(s);
}


foreach (var variation in new VariationsWithRepetitions<ushort>(new ushort[] { 0, 1 }, 12))
{
    Console.WriteLine(variation.ToString());
}