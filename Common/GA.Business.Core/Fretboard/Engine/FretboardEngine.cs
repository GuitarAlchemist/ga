namespace GA.Business.Core.Fretboard.Engine;

public class FretboardEngine
{
    /*
       Class goals
       - Maintain state(s)
       - Present positions (Chromatic by default)
       - Manage contexts (Emergent properties for the various groups of position)
         . Scales
         . Keys / Modes
         . Key/Open Positions relationship, Key/Capo relationship
         . Analyze note intervals (Modal point of view)
       - 
     */

    public FretboardEngineState State { get; set; } = new();
}