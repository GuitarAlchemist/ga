namespace GA.Domain.Core.Instruments.Shapes.CategoryTheory;

using JetBrains.Annotations;

/// <summary>
///     A functor between musical categories
/// </summary>
/// <typeparam name="TSource">Source category objects</typeparam>
/// <typeparam name="TTarget">Target category objects</typeparam>
/// <typeparam name="TSourceMorphism">Source category morphisms</typeparam>
/// <typeparam name="TTargetMorphism">Target category morphisms</typeparam>
/// <remarks>
///     A functor F: C ? D maps:
///     - Objects: F(A) for each object A in C
///     - Morphisms: F(f: A ? B) = F(f): F(A) ? F(B)
///     Functor laws:
///     1. Identity: F(id_A) = id_F(A)
///     2. Composition: F(g ° f) = F(g) ° F(f)
///     Musical examples:
///     - Transposition: PitchClass ? PitchClass
///     - Inversion: Interval ? Interval
///     - Voicing: PitchClassSet ? ChordVoicing
///     - Fingering: ChordVoicing ? FretboardShape
///     References:
///     - Mac Lane, S. (1998). Categories for the Working Mathematician
///     - Mazzola, G. (2002). The Topos of Music
/// </remarks>
[PublicAPI]
public interface IMusicalFunctor<TSource, TTarget, in TSourceMorphism, out TTargetMorphism>
{
    /// <summary>
    ///     Map an object from source category to target category
    /// </summary>
    TTarget MapObject(TSource source);

    /// <summary>
    ///     Map a morphism from source category to target category
    /// </summary>
    /// <remarks>
    ///     Must preserve composition: MapMorphism(g ° f) = MapMorphism(g) ° MapMorphism(f)
    /// </remarks>
    TTargetMorphism MapMorphism(TSourceMorphism morphism);
}