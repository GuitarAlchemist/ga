namespace GA.Core;

public record Ordinal(int Value)
{
    public static implicit operator Ordinal(int value) => new(value);
    public static implicit operator int(Ordinal ordinal) => ordinal.Value;

    public override string ToString() => Value switch
    {
        1 => "1st",
        2 => "2nd",
        _ => $"{Value}th",
    };
}