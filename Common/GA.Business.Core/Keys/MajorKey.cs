using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Keys;

[PublicAPI]
[DiscriminatedUnion]
public abstract partial record Key(
    KeyMode Mode,
    KeyAccidentals Accidentals)
{
    public sealed partial record Major(KeyAccidentals Accidentals) : Key(KeyMode.Major, Accidentals);
    public sealed partial record Minor(KeyAccidentals Accidentals) : Key(KeyMode.Minor, Accidentals);
}