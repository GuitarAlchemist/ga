namespace GA.Business.Core.Atonal;

public class ForteNumber : IEquatable<ForteNumber>, IComparable<ForteNumber>, IComparable
{
    #region Equality Members
    
    public bool Equals(ForteNumber? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Cardinality == other.Cardinality && Index == other.Index;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((ForteNumber)obj);
    }

    public override int GetHashCode() => HashCode.Combine(Cardinality, Index);

    
    #endregion

    #region Comparison Members

    public int CompareTo(ForteNumber? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        var cardinalityComparison = Cardinality.CompareTo(other.Cardinality);
        return cardinalityComparison != 0 
            ? cardinalityComparison 
            : string.Compare(Index, other.Index, StringComparison.Ordinal);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (ReferenceEquals(this, obj)) return 0;
        if (obj is not ForteNumber number) throw new ArgumentException($"Object must be of type {nameof(ForteNumber)}");
        return CompareTo(number);
    }    

    #endregion    
    
    public ForteNumber(int cardinality, string index)
    {
        if (string.IsNullOrWhiteSpace(index)) throw new ArgumentException("Index cannot be null or whitespace", nameof(index));
        
        Cardinality = cardinality;
        Index = index;
    }

    /// <summary>
    /// Gets the <see cref="int"/> cardinality
    /// </summary>
    public int Cardinality { get; }
    
    /// <summary>
    /// Gets the <see cref="string"/> index
    /// </summary>
    public string Index { get; }
    
    /// <inheritdoc />
    public override string ToString() => $"{Cardinality}-{Index}";

    public static ForteNumber Parse(string forteNumberString)
    {
        var parts = forteNumberString.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var cardinality)) throw new ArgumentException("Invalid Forte number format", nameof(forteNumberString));

        return new ForteNumber(cardinality, parts[1]);
    }
}
