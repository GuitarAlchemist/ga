namespace GA.InteractiveExtension.ExtensionMethods;

using Domain.Core.Primitives.Notes;
using Domain.Core.Theory.Harmony;
using JetBrains.Annotations;
using Markdown;

[PublicAPI]
public static class VisualizationExtensions
{
    extension(Note note)
    {
        /// <summary>
        ///     Converts a note to VexTab notation for rendering.
        /// </summary>
        public VexTabMarkDown ToVexTab() => new($"notes :q {note}/4");
    }

    extension(Chord chord)
    {
        /// <summary>
        ///     Converts a chord to VexTab notation for rendering.
        /// </summary>
        public VexTabMarkDown ToVexTab()
        {
            var notes = string.Join(".", chord.Notes.Select(n => $"{n}/4"));
            return new($"notes :q ({notes})");
        }
    }
}
