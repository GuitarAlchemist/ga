namespace GA.Business.Core.Tonal.Modes;

using System.Collections.Immutable;
using Scales;
using Primitives;

[PublicAPI]
public sealed class MajorPentatonicMode : ScaleMode<MajorPentatonicScaleDegree>
{
    public static IReadOnlyCollection<MajorPentatonicMode> All => MajorPentatonicScaleDegree.All.Select(degree => new MajorPentatonicMode(degree)).ToImmutableList();

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
}