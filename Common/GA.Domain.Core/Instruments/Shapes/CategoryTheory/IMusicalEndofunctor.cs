namespace GA.Domain.Core.Instruments.Shapes.CategoryTheory;

/// <summary>
///     Simplified functor for endofunctors (F: C ? C)
/// </summary>
[PublicAPI]
public interface IMusicalEndofunctor<T, TMorphism>
{
    /// <summary>
    ///     Map an object to another object in the same category
    /// </summary>
    T Map(T source);

    /// <summary>
    ///     Map a morphism to another morphism in the same category
    /// </summary>
    TMorphism MapMorphism(TMorphism morphism);
}