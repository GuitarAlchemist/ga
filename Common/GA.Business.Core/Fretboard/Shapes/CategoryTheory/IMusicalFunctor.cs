namespace GA.Business.Core.Fretboard.Shapes.CategoryTheory;

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

/// <summary>
///     A monad for composing musical transformations
/// </summary>
/// <typeparam name="T">The type being transformed</typeparam>
/// <remarks>
///     A monad is a functor M with two natural transformations:
///     - ? (unit/return): Id ? M - wraps a value
///     - µ (join/flatten): M ° M ? M - flattens nested structure
///     Monad laws:
///     1. Left identity: µ ° ?_M = id
///     2. Right identity: µ ° M(?) = id
///     3. Associativity: µ ° M(µ) = µ ° µ_M
///     Musical applications:
///     - Compose transformations with error handling
///     - Chain voice leading operations
///     - Sequence harmonic progressions
///     - Handle optional/multiple voicings
///     Example: Maybe monad for optional voicings
///     - Some(voicing) if voicing exists
///     - None if no valid voicing
/// </remarks>
[PublicAPI]
public interface IMusicalMonad<T>
{
    /// <summary>
    ///     Extract the value (if present)
    /// </summary>
    T? Value { get; }

    /// <summary>
    ///     Is the value present?
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    ///     Unit/Return: Wrap a value in the monad
    /// </summary>
    IMusicalMonad<T> Unit(T value);

    /// <summary>
    ///     Bind/FlatMap: Chain operations
    /// </summary>
    /// <remarks>
    ///     Bind allows sequencing operations:
    ///     m.Bind(f).Bind(g) = m.Bind(x => f(x).Bind(g))
    /// </remarks>
    IMusicalMonad<TResult> Bind<TResult>(Func<T, IMusicalMonad<TResult>> f);
}

/// <summary>
///     Extension methods for monadic operations
/// </summary>
[PublicAPI]
public static class MonadExtensions
{
    // NOTE: These extension methods have type system issues and are commented out
    // The IMusicalMonad<T>.Unit method returns IMusicalMonad<T>, not IMusicalMonad<TResult>
    // This makes it impossible to implement Map and Sequence correctly without changing the interface

    // /// <summary>
    // /// Map/FMap: Apply a function inside the monad
    // /// </summary>
    // public static IMusicalMonad<TResult> Map<T, TResult>(
    //     this IMusicalMonad<T> monad,
    //     Func<T, TResult> f)
    // {
    //     // Cannot implement - Unit returns wrong type
    //     throw new NotImplementedException();
    // }

    // /// <summary>
    // /// Sequence: Convert list of monads to monad of list
    // /// </summary>
    // public static IMusicalMonad<IReadOnlyList<T>> Sequence<T>(
    //     this IEnumerable<IMusicalMonad<T>> monads)
    // {
    //     // Cannot implement - Unit returns wrong type
    //     throw new NotImplementedException();
    // }
}

/// <summary>
///     Adjunction between two functors
/// </summary>
/// <remarks>
///     An adjunction F ? G consists of:
///     - Left adjoint functor F: C ? D
///     - Right adjoint functor G: D ? C
///     - Natural bijection: Hom_D(F(A), B) ? Hom_C(A, G(B))
///     Musical example:
///     - F: Add notes (PitchClassSet ? PitchClassSet)
///     - G: Remove notes (PitchClassSet ? PitchClassSet)
///     - Adjunction captures the relationship between adding and removing
///     Properties:
///     - F preserves colimits, G preserves limits
///     - Unit ?: Id ? G ° F
///     - Counit e: F ° G ? Id
/// </remarks>
[PublicAPI]
public interface IAdjunction<TLeft, TRight>
{
    /// <summary>
    ///     Left adjoint functor F
    /// </summary>
    IMusicalFunctor<TLeft, TRight, object, object> LeftAdjoint { get; }

    /// <summary>
    ///     Right adjoint functor G
    /// </summary>
    IMusicalFunctor<TRight, TLeft, object, object> RightAdjoint { get; }

    /// <summary>
    ///     Unit: Id ? G ° F
    /// </summary>
    TLeft Unit(TLeft source);

    /// <summary>
    ///     Counit: F ° G ? Id
    /// </summary>
    TRight Counit(TRight target);
}

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
