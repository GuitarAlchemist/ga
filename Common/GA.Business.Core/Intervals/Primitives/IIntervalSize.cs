namespace GA.Business.Core.Intervals.Primitives;

/// <summary>
/// See https://Objects.utk.edu/theorycomp/courses/murphy/documents/Intervals.pdf
/// </summary>
public interface IIntervalSize : IValueObject
{
    /// <summary>
    /// Gets the <see cref="IntervalSizeConsonance"/>
    /// </summary>
    IntervalSizeConsonance Consonance { get; }

    Semitones ToSemitones();
}

[PublicAPI]
public interface IIntervalSize<TSelf> : IIntervalSize,
                                          IValueObject<TSelf>,
                                          IValueObjectCollection<TSelf>
    where TSelf : struct,
                  IIntervalSize<TSelf>
{
}