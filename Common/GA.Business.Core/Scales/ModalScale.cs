namespace GA.Business.Core.Scales;

using Notes;
using Tonal.Modes;

public class ModalScale : Scale, IModalScale
{
    private readonly Lazy<IReadOnlyList<ScaleMode>> _lazyModes;

    public ModalScale(IEnumerable<Note> notes) : base(notes)
    {
        if (!IsModal) throw new ArgumentException("The provided notes do not form a modal scale.", nameof(notes));
        _lazyModes = new(CreateModes);
    }

    public IReadOnlyCollection<ScaleMode> Modes => _lazyModes.Value;

    private ImmutableList<GenericScaleMode> CreateModes() => Enumerable.Range(1, Count).Select(degree => new GenericScaleMode(this, degree)).ToImmutableList();

    public override string ToString() => $"Modal {base.ToString()}";
}