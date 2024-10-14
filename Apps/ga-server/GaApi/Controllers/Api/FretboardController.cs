namespace GaApi.Controllers.Api;

using System.Collections.Immutable;
using GA.Business.Core.Atonal;
using GA.Business.Core.Extensions;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Engine;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes;

[ApiController]
[Route("[controller]")]
public class FretboardController : ControllerBase
{
    [HttpGet(Name = "Positions")]
    public IEnumerable<string> Get(
        string? notes,
        bool showDetails)
    {
        var gen = new FretboardChordsGenerator(Fretboard.Default);
        PitchClassSet? pitchClassSetFilter = null;
        if (AccidentedNoteCollection.TryParse(notes, null, out var accidentedNotes)) pitchClassSetFilter = accidentedNotes.ToPitchClassSet();
        foreach (var positions in gen.GetChordPositions(pitchClassSetFilter))
        {
            yield return showDetails
                ? $"{GetSummary(positions)} - {GetDetails(positions)}"
                : GetSummary(positions);
        }
        yield break;

        string GetSummary(ImmutableList<Position> item) => string.Join(" ", item.Select(position => $"{position.Location.Fret.ToString(),-2}"));
        string GetDetails(IEnumerable<Position> positions) => $"{string.Join(", ", positions.Select(position => $"{{{position}}}"))}";
    }
}