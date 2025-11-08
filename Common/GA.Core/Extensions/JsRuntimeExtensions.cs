namespace GA.Core.Extensions;

using Microsoft.JSInterop;

public static class JsRuntimeExtensions
{
    public static async Task LogAsync(
        this IJSRuntime jsRuntime,
        string message)
    {
        await jsRuntime.InvokeVoidAsync("console.log", message);
    }

    public static async Task ErrorAsync(
        this IJSRuntime jsRuntime,
        string message)
    {
        await jsRuntime.InvokeVoidAsync("console.error", message);
    }
}
