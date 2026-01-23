namespace GA.Business.Core.Session;

using System;
using System.Threading;
using Domain.Core.Session;
using JetBrains.Annotations;

/// <summary>
/// In-memory implementation of session context provider
/// </summary>
/// <remarks>
/// Thread-safe implementation using lock for concurrent access.
/// Suitable for single-user scenarios (desktop apps, single-thread web requests).
/// </remarks>
[PublicAPI]
public sealed class InMemorySessionContextProvider : ISessionContextProvider
{
    private readonly object _lock = new();
    private MusicalSessionContext _currentContext;
    
    /// <summary>
    /// Creates a new in-memory session context provider with default context
    /// </summary>
    public InMemorySessionContextProvider()
    {
        _currentContext = MusicalSessionContext.Default();
    }
    
    /// <summary>
    /// Creates a new in-memory session context provider with specified initial context
    /// </summary>
    public InMemorySessionContextProvider(MusicalSessionContext initialContext)
    {
        _currentContext = initialContext ?? throw new ArgumentNullException(nameof(initialContext));
    }
    
    /// <inheritdoc />
    public event EventHandler<MusicalSessionContext>? ContextChanged;
    
    /// <inheritdoc />
    public MusicalSessionContext GetContext()
    {
        lock (_lock)
        {
            return _currentContext;
        }
    }
    
    /// <inheritdoc />
    public void UpdateContext(Func<MusicalSessionContext, MusicalSessionContext> updateFn)
    {
        if (updateFn == null) throw new ArgumentNullException(nameof(updateFn));
        
        MusicalSessionContext newContext;
        lock (_lock)
        {
            newContext = updateFn(_currentContext);
            _currentContext = newContext;
        }
        
        // Raise event outside of lock to avoid potential deadlocks
        OnContextChanged(newContext);
    }
    
    /// <inheritdoc />
    public void SetContext(MusicalSessionContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        
        lock (_lock)
        {
            _currentContext = context;
        }
        
        OnContextChanged(context);
    }
    
    /// <inheritdoc />
    public void ResetContext()
    {
        var defaultContext = MusicalSessionContext.Default();
        
        lock (_lock)
        {
            _currentContext = defaultContext;
        }
        
        OnContextChanged(defaultContext);
    }
    
    private void OnContextChanged(MusicalSessionContext context)
    {
        ContextChanged?.Invoke(this, context);
    }
}
