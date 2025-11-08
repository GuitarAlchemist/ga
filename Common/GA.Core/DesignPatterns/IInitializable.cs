namespace GA.Core.DesignPatterns;

/// <summary>
///     Initializable object abstraction (With inits).
/// </summary>
public interface IInitializable<in TInits>
{
    void Initialize(TInits inits);
}

/// <summary>
///     Initializable object abstraction (No inits).
/// </summary>
public interface IInitializable
{
    void Initialize();
}
