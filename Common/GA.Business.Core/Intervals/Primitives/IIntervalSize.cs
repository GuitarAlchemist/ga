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

/// <summary>
/// Interval size (Strongly typed)
/// </summary>
/// <remarks>
/// Derives from <see cref="IStaticValueObjectList{TSelf}"/>, <see cref="IIntervalSize"/>
/// </remarks>
/// <typeparam name="TSelf"></typeparam>
[PublicAPI]
public interface IIntervalSize<TSelf> : IStaticValueObjectList<TSelf>,
                                        IIntervalSize
    where TSelf : struct,
                  IIntervalSize<TSelf>
{
}