namespace GA.InteractiveExtension.Ga;

using static PocketViewTags;
using GA.Business.Core.Fretboard;

public static class FretboardFormatter
{
    public static IHtmlContent DrawFretboard(this Tuning tuning)
    {
        var id = "fretboard" + Guid.NewGuid().ToString("N");
        return div[id: id]();

        IHtmlContent Css() => new HtmlString($@"
#{id} svg {{
  width: 400px;

}}
    ");
    }
}
