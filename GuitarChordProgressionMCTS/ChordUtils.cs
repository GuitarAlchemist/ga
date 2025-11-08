namespace GuitarChordProgressionMCTS;

public static class ChordUtils
{
    // Determines if a chord voicing is a barre chord
    public static bool IsBarreChord(int[] voicing)
    {
        var consecutiveBarredStrings = 0;
        var previousFret = -1;

        for (var i = 0; i < voicing.Length; i++)
        {
            var fret = voicing[i];

            if (fret > 0)
            {
                if (fret == previousFret || previousFret == -1)
                {
                    consecutiveBarredStrings++;
                    if (consecutiveBarredStrings >= 2)
                    {
                        return true;
                    }
                }
                else
                {
                    consecutiveBarredStrings = 1;
                }

                previousFret = fret;
            }
            else
            {
                // Reset if open string or muted string
                consecutiveBarredStrings = 0;
                previousFret = -1;
            }
        }

        return false;
    }
}
