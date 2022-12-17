namespace GA.Business.Core.Extensions;

using Atonal;
using Intervals.Primitives;

public static class PitchClassExtensions
{
    /// <summary>
    /// Create the interval structure from pitch classes.
    /// </summary>
    /// <param name="pitchClasses">The <see cref="IEnumerable{PitchClass}"/>.</param>
    /// <returns>The <see cref="ImmutableList{Semitones}"/>.</returns>
    public static ImmutableList<Semitones> ToIntervalStructure(this IEnumerable<PitchClass> pitchClasses)
    {
        var pitchClassList = pitchClasses.ToList();
        var listBuilder = ImmutableList.CreateBuilder<Semitones>();
        var sum = 0;
        for (var index = 1; index < pitchClassList.Count; index++)
        {
            var value = pitchClassList[index].Value - pitchClassList[index - 1].Value;
            var item = Semitones.FromValue(value);
            listBuilder.Add(item);

            sum += value;
        }

        if (sum < 12) listBuilder.Add(Semitones.FromValue(12 - sum)); // Close the 12 semitones circle

        return listBuilder.ToImmutable();
    }
}
