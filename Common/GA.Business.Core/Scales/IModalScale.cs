namespace GA.Business.Core.Scales;

using Tonal.Modes;

public interface IModalScale
{
    IReadOnlyCollection<ScaleMode> Modes { get; }
}