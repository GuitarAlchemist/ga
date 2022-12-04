namespace GA.Business.Core.Intervals.Primitives;

using GA.Core.Collections;
using GA.Core;

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
public interface IIntervalSize<TSelf> : IStaticValueObjectList<TSelf>,
                                        IIntervalSize
    where TSelf : struct,
                  IIntervalSize<TSelf>
{
}