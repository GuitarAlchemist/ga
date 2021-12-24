namespace GA.Business.Core.Fretboard.Primitives;

[PublicAPI]
[DiscriminatedUnion]
public abstract partial record KeyAccidentals
{
    public const uint MinValue = 0;
    public const uint MaxValue = 7;
    private uint _count;

    public uint Count
    {
        get => _count;
        init
        {
            if (value > MaxValue) throw new ArgumentOutOfRangeException(nameof(Count), $"{nameof(Count)} must be less or equal to {MaxValue}");
            _count = value;
        }
    }

    public sealed partial record Sharps(uint Count);
    public sealed partial record Flats(uint Count);

    public static implicit operator uint(KeyAccidentals value) => value.Count;
}