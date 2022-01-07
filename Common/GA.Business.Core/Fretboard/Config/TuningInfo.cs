namespace GA.Business.Core.Fretboard.Config;

public record TuningInfo(string InstrumentName, string? TuningName = null)
{
    public override string ToString() => TuningName == null 
        ? InstrumentName
        : $"{InstrumentName} / {TuningName}";
}