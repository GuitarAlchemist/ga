using System.Collections;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard.Config;

[PublicAPI]
public record PositionsRange
{
    public static readonly PositionsRange Default = new(6, 22);

    private uint _stringCount;
    private uint _fretCount;

    /// <summary>
    /// Creates a <see cref="PositionsRange"/> instance.
    /// </summary>
    /// <param name="stringCount"></param>
    /// <param name="fretCount"></param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either <see cref="stringCount"/> or <see cref="fretCount"/> is out of range.</exception>
    public PositionsRange(
        uint stringCount, 
        uint fretCount)
    {
        Str.CheckRange(stringCount);
        Fret.CheckRange(fretCount);
    }

    public void Deconstruct(
        out int stringCount, 
        out int fretCount)
    {
        stringCount = (int) _stringCount;
        fretCount = (int) _fretCount;
    }

    public void Check(Str str)
    {
        str.CheckMaxValue(_stringCount);
    }

    public void Check(
        Str str, 
        Fret fret)
    {
        str.CheckMaxValue(_stringCount);
        fret.CheckMaxValue(_fretCount);
    }

    public uint StringCount
    {
        get => _stringCount;
        init => _stringCount = Str.CheckRange(value);
    }

    public uint FretCount
    {
        get => _fretCount;
        init => _fretCount = Fret.CheckRange(value);
    }

}