namespace GA.WebBlazorApp.Extensions;

using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

[PublicAPI]
public static class JsRuntimeExtensions
{
    extension(IJSRuntime jsRuntime)
    {
        public async Task OpenFullScreenAsync(ElementReference element)
        {
            if (jsRuntime == null)
            {
                throw new ArgumentNullException(nameof(jsRuntime));
            }

            await jsRuntime.InvokeVoidAsync("openFullScreen", element);
        }

        public async Task OpenFullScreenByIdAsync(string elementId)
        {
            if (jsRuntime == null)
            {
                throw new ArgumentNullException(nameof(jsRuntime));
            }

            await jsRuntime.InvokeVoidAsync("openFullScreenById", elementId);
        }

        public async Task CloseFullScreenAsync()
        {
            if (jsRuntime == null)
            {
                throw new ArgumentNullException(nameof(jsRuntime));
            }

            await jsRuntime.InvokeVoidAsync("closeFullScreen");
        }

        public async Task<bool> IsFullScreenAsync()
        {
            if (jsRuntime == null)
            {
                throw new ArgumentNullException(nameof(jsRuntime));
            }

            return await jsRuntime.InvokeAsync<bool>("isFullScreen");
        }

        public async Task AlertAsync(string message)
        {
            if (jsRuntime == null)
            {
                throw new ArgumentNullException(nameof(jsRuntime));
            }

            await jsRuntime.InvokeVoidAsync("alert", message);
        }

        public async Task DebounceEvent(
            ElementReference element,
            string eventName,
            TimeSpan delay)
        {
            await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/Modules/events.js");
            await module.InvokeVoidAsync("debounceEvent", element, eventName, (long)delay.TotalMilliseconds);
        }

        public async Task ThrottleEvent(
            ElementReference element,
            string eventName,
            TimeSpan delay)
        {
            await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/Modules/events.js");
            await module.InvokeVoidAsync("throttleEvent", element, eventName, (long)delay.TotalMilliseconds);
        }
    }
}
