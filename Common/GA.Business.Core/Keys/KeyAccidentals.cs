namespace GA.Business.Core.Keys;

[PublicAPI]
[DiscriminatedUnion]
public abstract partial record KeyAccidentals
{
    public static implicit operator int(KeyAccidentals value) => value.Count;

    public const int MinValue = 0;
    public const int MaxValue = 7;
    private int _count;

    public int Count
    {
        get => _count;
        init
        {
            if (value > MaxValue) throw new ArgumentOutOfRangeException(nameof(Count), $"{nameof(Count)} must be less or equal to {MaxValue}");
            _count = value;
        }
    }

    public partial record Sharps : KeyAccidentals;
    public partial record Flats : KeyAccidentals;
}