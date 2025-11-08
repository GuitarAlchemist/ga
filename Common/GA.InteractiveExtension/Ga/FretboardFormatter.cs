namespace GA.InteractiveExtension.Ga;

using Business.Core.Fretboard;

public static class FretboardFormatter
{
    public static string DrawFretboard(this Tuning tuning)
    {
        var id = "fretboard" + Guid.NewGuid().ToString("N");

        // Simple HTML representation of the fretboard
        var html = $@"
<div id='{id}' class='fretboard'>
    <h4>Tuning: {tuning}</h4>
    <div class='strings'>
        <div class='string-info'>Strings: {tuning.StringCount}</div>
        <div class='pitches'>{string.Join(" - ", tuning.AsSpan().ToArray().Select(p => p.ToString()))}</div>
    </div>
    <style>
        #{id} {{
            border: 1px solid #ccc;
            padding: 10px;
            margin: 5px;
            background-color: #f9f9f9;
            font-family: monospace;
        }}
        #{id} .strings {{
            margin-top: 10px;
        }}
        #{id} .string-info {{
            font-weight: bold;
            color: #333;
        }}
        #{id} .pitches {{
            color: #666;
            margin-top: 5px;
        }}
    </style>
</div>";

        return html;
    }
}
