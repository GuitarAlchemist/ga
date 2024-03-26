namespace GA.Business.Core.AI;

using GA.Core.Extensions;
using Tonal;

public class GaKeyPlugin
{
    [KernelFunction, Description("get all key signatures")]
    public static string GetKeySignatures() => KeySignature.Items.AsPrintable(", ").ToString();

    [KernelFunction, Description("get all keys")]
    public static string GetKeys() => Key.Items.AsPrintable(", ").ToString();
    
    [KernelFunction, Description("get accidentals in a given key signature")]
    public static string GetAccidentedNotesInKeySignature([Description("The key signature to get the accidented notes from")] KeySignature keySignature) => keySignature.AccidentedNotes.ToString();
    
    [KernelFunction, Description("get accidetals in a given key")]
    public static string GetAccidentedNotesInKey([Description("The key to get the accidentals from")] Key key) => key.KeySignature.AccidentedNotes.ToString();
}