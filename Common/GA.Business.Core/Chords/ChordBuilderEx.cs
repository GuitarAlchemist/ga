namespace GA.Business.Core.Chords;

using Atonal;
using Extensions;
using Notes;
using Scales;

public class ChordBuilderEx
{
    public static IEnumerable<Scale> FindScalesContainingChord(PitchClassSet chord)
    {
        foreach (var scale in CommonScales.Instance)
        {
            if (scale.IsModal)
            {
                var modalFamily = scale.ModalFamily;
            }
            else
            {
                
            }
        }

        return [];
    }
}