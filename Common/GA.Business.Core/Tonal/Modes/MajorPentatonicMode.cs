namespace GA.Business.Core.Tonal.Modes;

using Scales;
using Primitives;

[PublicAPI]
public sealed class MajorPentatonicMode : ScaleMode<MajorPentatonicScaleDegree>,
                                          IMusicObjectCollection<MajorPentatonicMode>
{
    public static IReadOnlyCollection<MajorPentatonicMode> All => MajorPentatonicScaleDegree.Items.Select(degree => new MajorPentatonicMode(degree)).ToImmutableList();

    public override string Name => ParentScaleDegree.Value switch
    {
        1 => "Major pentatonic",
        2 => "Egyptian",
        3 => "Blues minor",
        4 => "Blues major",
        5 => "Minor pentatonic",
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };

    public override string ToString() => $"{Name} - {Formula}";

    public MajorPentatonicMode(MajorPentatonicScaleDegree degree)
        : base(Scale.MajorPentatonic, degree)
    {
    }

    public static IEnumerable<MajorPentatonicMode> Objects => MajorPentatonicScaleDegree.Items.Select(degree => new MajorPentatonicMode(degree));
    public static MajorPentatonicMode Get(MajorPentatonicScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static MajorPentatonicMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<MajorPentatonicScaleDegree, MajorPentatonicMode>> _lazyModeByDegree = new(() => new(Objects.ToImmutableList()));

}