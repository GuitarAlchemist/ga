﻿
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
    private void Stuff1()
    {
        var identities = PitchClassSetIdentity.Items;
        var pcsObjects = PitchClassSet.Objects;
        var byCard = pcsObjects.GroupBy(set => set.Cardinality).ToImmutableList();
        foreach (var cardGroup in byCard)
        {
            var first = cardGroup.First();
            Console.WriteLine(first.Cardinality.ToString());
            var url = first.Identity.ScalePageUrl;
            Console.WriteLine(url);
        }

        var groups = pcsObjects.GroupBy(set => set.IntervalClassVector).ToImmutableList();

        var pf = PitchClassSet.PrimeForms;
        var id = PitchClassSetIdentity.FromNotes(Note.Chromatic.C, Note.Chromatic.E, Note.Chromatic.GSharpAb);
        var idTranspositions = id.PitchClassSet.Transpositions;

        var scales = Scale.Objects.ToImmutableList();
        var modalScales =
            scales.Where(scale => scale.Identity.PitchClassSet.IsModal)
                .OrderBy(scale => scale.Count)
                .ToImmutableList();

        var icv = Scale.Major.Identity.PitchClassSet.IntervalClassVector;
        var modes = scales.Where(scale => scale.Identity.PitchClassSet.IntervalClassVector == icv).ToImmutableList();

        var dorian = MajorScaleMode.Dorian;
        var formula = dorian.Formula;
        var colorNotes = dorian.ColorNotes;
        var dorianNotes = dorian.Notes;
        var dorianIdentity = MajorScaleMode.Dorian.Identity;
        var isModal = dorianIdentity.PitchClassSet.IsModal;
        var modalFamily =
            (ModalFamily?)(ModalFamily.TryGetValue(dorianIdentity.PitchClassSet.IntervalClassVector, out var modalFamily1)
                ? modalFamily1
                : null);
        var aaaa = 42;

        //var intervals = new List<(Note, Note,Interval.Simple)>();
        //for (var i = 0; i < notes.Count; i++)
        //{
        //    var note1 = notes.ElementAt(i);
        //    var i2 = (i + 6) % notes.Count;
        //    var note2 =  notes.ElementAt(i2);
        //    var interval = note1.GetInterval(note2);
        //    intervals.Add((note1, note2, interval));
        //}

        //var sIntervals = intervals.ToString();

        /*

        Ideas: 
        Decompose horizontal movement into m3/M3 fret intervals - See https://www.youtube.com/watch?v=Ab3nqlbl9us

        */

        // RenderFretboard();

        Console.ReadKey();

    }

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