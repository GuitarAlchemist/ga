namespace GA.Domain.Core.Primitives;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Design;
using Extensions;
using GA.Core.Abstractions;
using JetBrains.Annotations;
using Theory.Atonal;
using Theory.Atonal.Abstractions;

[PublicAPI]
[DomainRelationship(typeof(PitchClass), RelationshipType.IsParentOf, "A note contains a pitch class as one of its components")]
public abstract record Note : IStaticPairNorm<Note, IntervalClass>,
    IComparable<Note>,
    IPitchClass
{
    public abstract PitchClass PitchClass { get; }

    public static IntervalClass GetPairNorm(Note item1, Note item2)
    {
        return item1.GetIntervalClass(item2);
    }

    public abstract Accidented ToAccidented();

    public Chromatic ToChromatic() => new(PitchClass);

    public int CompareTo(Note? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        return PitchClass.CompareTo(other.PitchClass);
    }

    [PublicAPI]
    public sealed record Chromatic(int Value) : Note
    {
        public override PitchClass PitchClass { get; } = Value;

        public override Accidented ToAccidented() => ToSharp().ToAccidented();

        public Sharp ToSharp() => PitchClass.ToSharpNote();
        public Flat ToFlat() => PitchClass.ToFlatNote();

        public override string ToString()
        {
            var sharp = ToSharp();
            return !sharp.SharpAccidental.HasValue
                ? $"{sharp}" 
                : $"{sharp}/{ToFlat()}";
        }

        public static Chromatic C => new(0);
        public static Chromatic CSharpOrDFlat => new(1);
        public static Chromatic D => new(2);
        public static Chromatic DSharpOrEFlat => new(3);
        public static Chromatic E => new(4);
        public static Chromatic F => new(5);
        public static Chromatic FSharpOrGFlat => new(6);
        public static Chromatic G => new(7);
        public static Chromatic GSharpOrAFlat => new(8);
        public static Chromatic A => new(9);
        public static Chromatic ASharpOrBFlat => new(10);
        public static Chromatic B => new(11);

        public static implicit operator Chromatic(int value) => new(value);
        public static implicit operator Chromatic(PitchClass pc) => new(pc.Value);
    }

    [PublicAPI]
    public abstract record KeyNote(NaturalNote NaturalNote) : Note
    {
        public abstract Accidental? Accidental { get; }
        public override PitchClass PitchClass => GetPitchClass();
        protected abstract PitchClass GetPitchClass();
        public override Accidented ToAccidented() => new(NaturalNote, Accidental);

        public static bool TryParse(string? input, out IReadOnlyCollection<KeyNote> keyNotes)
        {
            var builder = ImmutableList.CreateBuilder<KeyNote>();
            if (Sharp.TryParse(input, null, out var sharp)) builder.Add(sharp);
            if (Flat.TryParse(input, null, out var flat)) builder.Add(flat);
            keyNotes = builder.ToImmutable();
            return keyNotes.Count > 0;
        }
    }

    [PublicAPI]
    public sealed record Sharp(NaturalNote NaturalNote, SharpAccidental? SharpAccidental = null) : KeyNote(NaturalNote), IParsable<Sharp>
    {
        public override Accidental? Accidental => SharpAccidental;
        protected override PitchClass GetPitchClass() => 
            NaturalNote.PitchClass + (SharpAccidental?.Value ?? 0);

        public override string ToString() => $"{NaturalNote}{SharpAccidental}";

        public static IReadOnlyCollection<Sharp> Items => [C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp, B];

        public static Sharp C => new(NaturalNote.C);
        public static Sharp CSharp => new(NaturalNote.C, Primitives.SharpAccidental.Sharp);
        public static Sharp D => new(NaturalNote.D);
        public static Sharp DSharp => new(NaturalNote.D, Primitives.SharpAccidental.Sharp);
        public static Sharp E => new(NaturalNote.E);
        public static Sharp F => new(NaturalNote.F);
        public static Sharp FSharp => new(NaturalNote.F, Primitives.SharpAccidental.Sharp);
        public static Sharp G => new(NaturalNote.G);
        public static Sharp GSharp => new(NaturalNote.G, Primitives.SharpAccidental.Sharp);
        public static Sharp A => new(NaturalNote.A);
        public static Sharp ASharp => new(NaturalNote.A, Primitives.SharpAccidental.Sharp);
        public static Sharp B => new(NaturalNote.B);

        public static Sharp Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var result) ? result : throw new FormatException();
        public static bool TryParse(string? s, IFormatProvider? provider, out Sharp result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var norm = s.Trim().ToUpperInvariant().Replace("♯", "#");
            try {
                if (norm.EndsWith("#")) {
                     if (NaturalNote.TryParse(norm[0].ToString(), null, out var nn)) {
                         result = new Sharp(nn, Primitives.SharpAccidental.Sharp);
                         return true;
                     }
                } else {
                     if (NaturalNote.TryParse(norm, null, out var nn)) {
                         result = new Sharp(nn);
                         return true;
                     }
                }
            } catch {}
            return false;
        }
        
        public static implicit operator Chromatic(Sharp s) => s.ToChromatic();
    }

    [PublicAPI]
    public sealed record Flat(NaturalNote NaturalNote, FlatAccidental? FlatAccidental = null) : KeyNote(NaturalNote), IParsable<Flat>
    {
        public override Accidental? Accidental => FlatAccidental;
        protected override PitchClass GetPitchClass() => 
            NaturalNote.PitchClass + (FlatAccidental?.Value ?? 0);

        public override string ToString() => $"{NaturalNote}{FlatAccidental}";

        public static IReadOnlyCollection<Flat> Items => [C, CFlat, D, DFlat, E, EFlat, F, FFlat, G, GFlat, A, AFlat, B, BFlat];

        public static Flat C => new(NaturalNote.C);
        public static Flat CFlat => new(NaturalNote.C, Primitives.FlatAccidental.Flat);
        public static Flat D => new(NaturalNote.D);
        public static Flat DFlat => new(NaturalNote.D, Primitives.FlatAccidental.Flat);
        public static Flat E => new(NaturalNote.E);
        public static Flat EFlat => new(NaturalNote.E, Primitives.FlatAccidental.Flat);
        public static Flat F => new(NaturalNote.F);
        public static Flat FFlat => new(NaturalNote.F, Primitives.FlatAccidental.Flat);
        public static Flat G => new(NaturalNote.G);
        public static Flat GFlat => new(NaturalNote.G, Primitives.FlatAccidental.Flat);
        public static Flat A => new(NaturalNote.A);
        public static Flat AFlat => new(NaturalNote.A, Primitives.FlatAccidental.Flat);
        public static Flat B => new(NaturalNote.B);
        public static Flat BFlat => new(NaturalNote.B, Primitives.FlatAccidental.Flat);

        public static Flat Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var result) ? result : throw new FormatException();
        public static bool TryParse(string? s, IFormatProvider? provider, out Flat result)
        {
             result = null!;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var norm = s.Trim().ToUpperInvariant().Replace("♭", "b");
             try {
                if (norm.EndsWith("B")) {
                     if (NaturalNote.TryParse(norm[0].ToString(), null, out var nn)) {
                         result = new Flat(nn, Primitives.FlatAccidental.Flat);
                         return true;
                     }
                } else {
                     if (NaturalNote.TryParse(norm, null, out var nn)) {
                         result = new Flat(nn);
                         return true;
                     }
                }
            } catch {}
            return false;
        }
        
        public static implicit operator Chromatic(Flat s) => s.ToChromatic();
    }

    [PublicAPI]
    public sealed record Accidented(NaturalNote NaturalNote, Accidental? Accidental = null) : Note, IParsable<Accidented>
    {
        public override PitchClass PitchClass => NaturalNote.PitchClass + (Accidental?.Value ?? 0);
        public override Accidented ToAccidented() => this;
        public override string ToString() => $"{NaturalNote}{Accidental}";

        public static IReadOnlyCollection<Accidented> Items => 
            Sharp.Items.Select(s => new Accidented(s.NaturalNote, s.Accidental))
            .Concat(Flat.Items.Select(f => new Accidented(f.NaturalNote, f.Accidental)))
            .Distinct()
            .ToList();

        public static Accidented Parse(string s, IFormatProvider? provider) => TryParse(s, provider, out var result) ? result : throw new FormatException();
        public static bool TryParse(string? s, IFormatProvider? provider, out Accidented result)
        {
             result = null!;
             if (string.IsNullOrWhiteSpace(s)) return false;
             var nnStr = s[0].ToString();
             if (NaturalNote.TryParse(nnStr, null, out var nn))
             {
                 var accStr = s.Substring(1);
                 Accidental? acc = null;
                 if (!string.IsNullOrEmpty(accStr))
                 {
                     if (Primitives.Accidental.TryParse(accStr, null, out var a)) acc = a;
                     else return false;
                 }
                 result = new Accidented(nn, acc);
                 return true;
             }
             return false;
        }

        public static implicit operator Chromatic(Accidented s) => s.ToChromatic();
        public static implicit operator Accidented(KeyNote k) => k.ToAccidented();
        
        public static Accidented C => new(NaturalNote.C);
        public static Accidented CSharp => new(NaturalNote.C, SharpAccidental.Sharp);
    }
}