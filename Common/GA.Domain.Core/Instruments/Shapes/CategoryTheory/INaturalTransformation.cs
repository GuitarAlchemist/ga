namespace GA.Domain.Core.Instruments.Shapes.CategoryTheory;

/// <summary>
///     A natural transformation between two functors
/// </summary>
/// <typeparam name="TSource">Source category objects</typeparam>
/// <typeparam name="TTarget">Target category objects</typeparam>
/// <remarks>
///     A natural transformation ?: F ? G between functors F, G: C ? D
///     assigns to each object A in C a morphism ?_A: F(A) ? G(A)
///     such that for every morphism f: A ? B in C:
///     G(f) ° ?_A = ?_B ° F(f)
///     (Naturality square commutes)
///     Musical example:
///     - Different voicing strategies (F and G) for the same chord
///     - Natural transformation ensures voice leading is preserved
/// </remarks>
[PublicAPI]
public interface INaturalTransformation<TSource, TTarget>
{
    /// <summary>
    ///     Source functor F
    /// </summary>
    IMusicalFunctor<TSource, TTarget, object, object> SourceFunctor { get; }

    /// <summary>
    ///     Target functor G
    /// </summary>
    IMusicalFunctor<TSource, TTarget, object, object> TargetFunctor { get; }

    /// <summary>
    ///     Component at object A: ?_A: F(A) ? G(A)
    /// </summary>
    TTarget Component(TSource source);
}