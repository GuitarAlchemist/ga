namespace GA.Domain.Core.Instruments.Shapes.CategoryTheory;

/// <summary>
///     A category with objects and morphisms
/// </summary>
[PublicAPI]
public interface IMusicalCategory<TObject, TMorphism>
{
    /// <summary>
    ///     Identity morphism for an object
    /// </summary>
    TMorphism Identity(TObject obj);

    /// <summary>
    ///     Compose two morphisms: g ° f
    /// </summary>
    /// <remarks>
    ///     Must be associative: h ° (g ° f) = (h ° g) ° f
    /// </remarks>
    TMorphism Compose(TMorphism g, TMorphism f);

    /// <summary>
    ///     Source object of a morphism
    /// </summary>
    TObject Source(TMorphism morphism);

    /// <summary>
    ///     Target object of a morphism
    /// </summary>
    TObject Target(TMorphism morphism);
}