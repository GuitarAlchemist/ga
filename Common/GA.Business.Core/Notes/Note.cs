namespace GA.Business.Core.Notes;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Note
{
    /// <summary>
    /// Gets the <see cref="PitchClass"/>.
    /// </summary>
    public abstract PitchClass PitchClass { get; }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A chromatic note.
    /// </summary>
    public sealed partial record Chromatic()
        : Note
    {
        public static Chromatic Value0 => new(0);
        public static Chromatic Value1 => new(1);
        public static Chromatic Value2 => new(2);
        public static Chromatic Value3 => new(3);
        public static Chromatic Value4 => new(4);
        public static Chromatic Value5 => new(5);
        public static Chromatic Value6 => new(6);
        public static Chromatic Value7 => new(7);
        public static Chromatic Value8 => new(8);
        public static Chromatic Value9 => new(9);
        public static Chromatic Value10 => new(10);
        public static Chromatic Value11 => new(11);

        public Chromatic(PitchClass pitchClass) : this()
        {
            PitchClass = pitchClass;
        }

        public override PitchClass PitchClass { get; }
        public Sharp ToSharp() => PitchClass.ToSharpNote();
        public Flat ToFlat() => PitchClass.ToFlatNote();

        public override string ToString()
        {
            var sharp = ToSharp();
            var flat = ToFlat();
            return sharp.SharpAccidental.HasValue ? $"{sharp}/{flat}" : $"{sharp}";
        }
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A sharp note.
    /// </summary>
    public sealed partial record Sharp(
            NaturalNote NaturalNote,
            SharpAccidental? SharpAccidental = null)
        : Note
    {
        public static Sharp C => new(NaturalNote.C);
        public static Sharp CSharp => new(NaturalNote.C, Notes.SharpAccidental.Sharp);
        public static Sharp D => new(NaturalNote.D);
        public static Sharp DSharp => new(NaturalNote.D, Notes.SharpAccidental.Sharp);
        public static Sharp E => new(NaturalNote.E);
        public static Sharp F => new(NaturalNote.F);
        public static Sharp FSharp => new(NaturalNote.F, Notes.SharpAccidental.Sharp);
        public static Sharp G => new(NaturalNote.G);
        public static Sharp GSharp => new( NaturalNote.G, Notes.SharpAccidental.Sharp);
        public static Sharp A => new( NaturalNote.A);
        public static Sharp ASharp => new( NaturalNote.A, Notes.SharpAccidental.Sharp);
        public static Sharp B => new(NaturalNote.B);

        public override PitchClass PitchClass => NaturalNote.GetPitchClass() + (SharpAccidental?.Value ?? 0);

        public override string ToString() =>
            SharpAccidental.HasValue
                ? $"{NaturalNote}{SharpAccidental.Value}"
                : $"{NaturalNote}";
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A sharp note.
    /// </summary>
    public sealed partial record Flat(
            NaturalNote NaturalNote,
            FlatAccidental? FlatAccidental = null)
        : Note
    {
        public static Flat CFlat => new(NaturalNote.C, Notes.FlatAccidental.Flat);
        public static Flat C => new( NaturalNote.C);
        public static Flat DFlat => new(NaturalNote.D, Notes.FlatAccidental.Flat);
        public static Flat D => new( NaturalNote.D);
        public static Flat EFlat => new(NaturalNote.E, Notes.FlatAccidental.Flat);
        public static Flat E => new(NaturalNote.E);
        public static Flat FFlat => new(NaturalNote.F, Notes.FlatAccidental.Flat);
        public static Flat F => new(NaturalNote.F);
        public static Flat GFlat => new(NaturalNote.G, Notes.FlatAccidental.Flat);
        public static Flat G => new(NaturalNote.G);
        public static Flat AFlat => new(NaturalNote.A, Notes.FlatAccidental.Flat);
        public static Flat A => new(NaturalNote.A);
        public static Flat BFlat => new( NaturalNote.B, Notes.FlatAccidental.Flat);
        public static Flat B => new( NaturalNote.B);

        public override PitchClass PitchClass => NaturalNote.GetPitchClass() + FlatAccidental?.Value ?? 0;

        public override string ToString() =>
            FlatAccidental.HasValue
                ? $"{NaturalNote}{FlatAccidental.Value}"
                : $"{NaturalNote}";

    }

}