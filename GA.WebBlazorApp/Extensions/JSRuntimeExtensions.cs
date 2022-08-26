namespace GA.WebBlazorApp.Extensions;

using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

[PublicAPI]
public static class JsRuntimeExtensions
{
    public static async Task OpenFullScreenAsync(
        this IJSRuntime jsRuntime, 
        ElementReference element)
    {
        if (jsRuntime == null) throw new ArgumentNullException(nameof(jsRuntime));

        await jsRuntime.InvokeVoidAsync("openFullScreen", element);
    }

    public static async Task OpenFullScreenByIdAsync(
        this IJSRuntime jsRuntime, 
        string elementId)
    {
        if (jsRuntime == null) throw new ArgumentNullException(nameof(jsRuntime));

        await jsRuntime.InvokeVoidAsync("openFullScreenById", elementId);
    }

    public static async Task CloseFullScreenAsync(
        this IJSRuntime jsRuntime)
    {
        if (jsRuntime == null) throw new ArgumentNullException(nameof(jsRuntime));

        await jsRuntime.InvokeVoidAsync("closeFullScreen");
    }

    public static async Task<bool> IsFullScreenAsync(this IJSRuntime jsRuntime)
    {
        if (jsRuntime == null) throw new ArgumentNullException(nameof(jsRuntime));

        return await jsRuntime.InvokeAsync<bool>("isFullScreen");
    }

    public static async Task AlertAsync(
        this IJSRuntime jsRuntime, 
        string message)
    {
        if (jsRuntime == null) throw new ArgumentNullException(nameof(jsRuntime));

        await jsRuntime.InvokeVoidAsync("alert", message);
    }

    public static async Task DebounceEvent(
        this IJSRuntime jsRuntime, 
        ElementReference element, 
        string eventName, 
        TimeSpan delay)
    {
        await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/Modules/events.js");
        await module.InvokeVoidAsync("debounceEvent", element, eventName, (long)delay.TotalMilliseconds);
    }

    public static async Task ThrottleEvent(
        this IJSRuntime jsRuntime, 
        ElementReference element, 
        string eventName, 
        TimeSpan delay)
    {
        await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/Modules/events.js");
        await module.InvokeVoidAsync("throttleEvent", element, eventName, (long)delay.TotalMilliseconds);
    }
}