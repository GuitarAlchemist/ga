namespace GA.Business.Core.Intervals;

using Notes;

/// <summary>
/// Service to calculate the interval between two notes
/// </summary>
public interface IIntervalCalculator
{
    Result<Interval, string> GetInterval(Note note1, Note note2);
}
