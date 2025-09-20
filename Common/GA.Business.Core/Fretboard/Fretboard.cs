﻿namespace GA.Business.Core.Fretboard;

using Positions;
using Primitives;
using static Primitives.Position;

/// <summary>
/// Represent available fretboard positions for a given instrument/tuning
/// </summary>
[PublicAPI]
public class Fretboard
{
    private readonly Lazy<PositionCollection> _lazyPositions;
    private readonly Lazy<RelativeFretVectorCollection> _lazyRelativePositions;
    private readonly Lazy<ImmutableList<PositionLocation>> _lazyPositionLocations;
    private readonly Lazy<ImmutableDictionary<PositionLocation, Position>> _lazyPositionByLocation;
    public static readonly Fretboard Default = new();

    public Fretboard() : this(Tuning.Default, 24) { }

    public Fretboard(
        Tuning tuning,
        int fretCount)
    {
        Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        FretCount = fretCount;
        StringCount = tuning.PitchCollection.Count;

        _lazyPositions = new(() => new(GetPositions()));
        _lazyPositionByLocation = new(GetPositionByLocation);
        _lazyRelativePositions = new(new RelativeFretVectorCollection(StringCount));
        _lazyPositionLocations = new(() => [..Positions.Select(pos => pos.Location)]);
        return;

        IEnumerable<Position> GetPositions()
        {
            foreach (var str in Strings)
            {
                // Muted
                yield return new Muted(str);

                // Played
                var frets = Fret.Range(Capo ?? 0, FretCount - 1);
                var midiNote = Tuning[str].MidiNote;
                foreach (var fret in frets)
                {
                    yield return new Played(new(str, fret), midiNote++);
                }
            }
        }

        ImmutableDictionary<PositionLocation, Position> GetPositionByLocation() =>
            Positions.ToImmutableDictionary(
                pos => pos.Location,
                pos => pos);
    }

    public Tuning Tuning { get; }
    public int StringCount { get; }
    public int FretCount { get; }
    public Fret? Capo { get; set; }

    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{Str}"/>
    /// </summary>
    public IReadOnlyCollection<Str> Strings => Str.Range(StringCount);

    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{Fret}"/>
    /// </summary>
    public IReadOnlyCollection<Fret> Frets => Fret.Range(Fret.Min.Value, FretCount);

    /// <summary>
    /// Gets the <see cref="PositionCollection"/>
    /// </summary>
    public PositionCollection Positions => _lazyPositions.Value;

    /// <summary>
    /// Gets the <see cref="ImmutableList{PositionLocation}"/>
    /// </summary>
    public ImmutableList<PositionLocation> PositionLocations => _lazyPositionLocations.Value;

    /// <summary>
    /// Gets the <see cref="ImmutableSortedSet{PositionLocation}"/> for a given fret range
    /// </summary>
    /// <param name="fretRange">The <see cref="FretRange"/></param>
    public ImmutableSortedSet<PositionLocation> GetPositionLocationsSet(FretRange fretRange)
        => [.. PositionLocations.Where(fretRange.Contains)];

    /// <summary>
    /// Gets the <see cref="RelativeFretVectorCollection"/>
    /// </summary>
    public RelativeFretVectorCollection RelativePositions => _lazyRelativePositions.Value;

    /// <summary>
    /// Tries to get a position from a location
    /// </summary>
    /// <param name="location">The position location</param>
    /// <param name="position">The position, if found</param>
    /// <returns>True if the position was found, false otherwise</returns>
    public bool TryGetPositionFromLocation(PositionLocation location, [MaybeNullWhen(false)] out Position position)
    {
        if (_lazyPositionByLocation.Value.TryGetValue(location, out position))
        {
            return true;
        }

        position = null;
        return false;
    }

    public override string ToString() => $"{Tuning} - {FretCount} frets";

    public string Render()
    {
        using var memoryStream = new MemoryStream();
        using TextWriter textWriter = new StreamWriter(memoryStream, Encoding.UTF8);
        FretboardTextWriterRenderer.Render(this, textWriter);
        var s = Encoding.UTF8.GetString(memoryStream.ToArray());
        memoryStream.Flush();
        return s;
    }
}