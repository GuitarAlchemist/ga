using GA.Business.Core.Fretboard.Config;

namespace GA.Business.Core.Fretboard;

[PublicAPI]
public class Fretboard
{
    private readonly Tuning _tuning = Tuning.Default;
    private readonly PositionsRange _positionsRange = PositionsRange.Default;
    private readonly FretboardPositions _fretboardPositions = new FretboardPositions(PositionsRange.Default);

    public Fretboard()
    {
        Tuning = Tuning.Default;
    }

    public Tuning Tuning { get; set; }
    public uint FretCount { get; set; }

}