using GA.Business.Core.Notes.Primitives;

namespace GA.Business.Core.Notes.Extensions;

[PublicAPI]
public static class PitchClassExtensions
{
    public static bool IsEnharmonicWith(this IPitchClass a, IPitchClass b)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));

        return a.PitchClass == b.PitchClass;
    }
}

