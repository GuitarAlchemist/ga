namespace GaApi.Hubs;

/// <summary>Limits on how long <see cref="ChatbotHub"/> waits on a chat run.</summary>
public sealed class ChatbotHubOptions
{
    /// <summary>
    /// The longest wait for the first piece of answer text, and then between two pieces.
    /// A run that stalls this long is cancelled and the caller gets an <c>Error</c>; a run
    /// that keeps streaming is never cut off however long its answer is.
    /// </summary>
    public TimeSpan StallTimeout { get; set; } = TimeSpan.FromSeconds(25);
}
