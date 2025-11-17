namespace GA.Business.Core.Unified;

using Atonal;

/// <summary>
///     Unified class: captures the set-theoretic identity shared by all rotations (modal members).
/// </summary>
public sealed class UnifiedModeClass(
    IntervalClassVector icv,
    PitchClassSet primeForm,
    bool isSymmetric,
    ModalFamily? family)
{
    public UnifiedModeId Id { get; } = new(icv.Id, primeForm.Id);
    public IntervalClassVector IntervalClassVector { get; } = icv;
    public PitchClassSet PrimeForm { get; } = primeForm;
    public bool IsSymmetric { get; } = isSymmetric;
    public ModalFamily? Family { get; } = family;
    public int Cardinality => PrimeForm.Count;
}
