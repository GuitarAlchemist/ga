namespace GA.Business.Core.Session;

using System;
using Domain.Core.Session;
using JetBrains.Annotations;

/// <summary>
/// Provides access to and management of the musical session context
/// </summary>
[PublicAPI]
public interface ISessionContextProvider
{
    /// <summary>
    /// Gets the current musical session context
    /// </summary>
    MusicalSessionContext GetContext();
    
    /// <summary>
    /// Updates the session context using an update function
    /// </summary>
    /// <param name="updateFn">Function that takes the current context and returns a new one</param>
    void UpdateContext(Func<MusicalSessionContext, MusicalSessionContext> updateFn);
    
    /// <summary>
    /// Sets the session context to a specific value
    /// </summary>
    void SetContext(MusicalSessionContext context);
    
    /// <summary>
    /// Resets context to defaults
    /// </summary>
    void ResetContext();
    
    /// <summary>
    /// Raised when context changes
    /// </summary>
    event EventHandler<MusicalSessionContext>? ContextChanged;
}
