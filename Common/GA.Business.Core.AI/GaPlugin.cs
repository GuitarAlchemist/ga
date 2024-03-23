namespace GA.Business.Core.AI;

using Notes;
using Tonal;
using Microsoft.SemanticKernel;
using System.ComponentModel;

public class GaPlugin
{
    [KernelFunction, Description("List all key signatures")]
    public static IReadOnlyCollection<KeySignature> AllKeySignatures()
    {
        return KeySignature.Items;
    }
    
    [KernelFunction, Description("List notes in a key signature")]
    public static IReadOnlyCollection<Note.KeyNote> KeySignatures(KeySignature keySignature)
    {
        return keySignature.SignatureNotes;
    }
}