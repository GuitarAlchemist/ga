namespace GA.Core;

public record Ordinal(int Value)
{
    public static implicit operator Ordinal(int value)
    {
        return new Ordinal(value);
    }

    public static implicit operator int(Ordinal ordinal)
    {
        return ordinal.Value;
    }

    public override string ToString()
    {
        return Value switch
        {
            1 => "1st",
            2 => "2nd",
            _ => $"{Value}th"
        };
    }
}
