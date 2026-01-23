namespace GA.Domain.Core.Instruments.Shapes.CategoryTheory;

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