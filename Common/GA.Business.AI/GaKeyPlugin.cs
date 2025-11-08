namespace GA.Business.AI;

public class GaKeyPlugin
{
    [KernelFunction]
    [Description("get all key signatures")]
    public static string GetKeySignatures()
    {
        var keysBySignature = Key.Items.ToLookup(key => key.KeySignature);

        var sb = new StringBuilder();
        foreach (var keySignature in KeySignature.Items)
        {
            if (sb.Length > 0)
            {
                sb.Append("; ");
            }

            var description = GetDescription(keySignature);
            var keys = string.Join(", ", keysBySignature[keySignature]);
            sb.Append($"{description}: {keySignature.AccidentedNotes} ({keys})");
        }

        return sb.ToString();

        string GetDescription(KeySignature item)
        {
            return item.AccidentalKind switch
            {
                AccidentalKind.Sharp when item.AccidentalCount == 0 => "No accidentals",
                AccidentalKind.Sharp => $"{item.AccidentalCount} sharps",
                AccidentalKind.Flat => $"{item.AccidentalCount} flats",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    [KernelFunction("get_keys")]
    [Description("get all musical keys")]
    [return: Description("An array of musical keys")]
    public static Task<List<Key>> GetKeys()
    {
        return Task.FromResult(Key.Items.ToList());
    }

    [KernelFunction("getAccidentedNotesInKeySignature")]
    [Description("get accidentals in a given key signature")]
    public static string GetAccidentedNotesInKeySignature(
        [Description("The key signature to get the accidented notes from")]
        KeySignature keySignature)
    {
        return keySignature.AccidentedNotes.ToString();
    }

    [KernelFunction("getAccidentedNotesInKey")]
    [Description("get accidentals in a given key")]
    public static string GetAccidentedNotesInKey([Description("The key to get the accidentals from")] Key key)
    {
        return key.KeySignature.AccidentedNotes.ToString();
    }
}
