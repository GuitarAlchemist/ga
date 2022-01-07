namespace GA.Business.Core.Intervals;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Interval
{
    /// <inheritdoc cref="Interval"/>
    /// <summary>
    /// A muted position.
    /// </summary>
    public sealed partial record Chromatic : Interval
    {
    }

    public sealed partial record Diatonic(Accidental? Accidental) : Interval
    {
    }
}