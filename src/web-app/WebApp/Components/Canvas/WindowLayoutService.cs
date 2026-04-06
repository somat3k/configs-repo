using System.Text.Json;
using Microsoft.JSInterop;

namespace MLS.WebApp.Components.Canvas;

/// <summary>Persist/restore MDI window layout via browser localStorage.</summary>
public interface IWindowLayoutService
{
    /// <summary>Serialises <paramref name="windows"/> to localStorage.</summary>
    Task SaveAsync(IEnumerable<WindowState> windows, CancellationToken ct);

    /// <summary>Deserialises the saved layout from localStorage. Returns empty array when nothing is stored.</summary>
    Task<WindowState[]> LoadAsync(CancellationToken ct);
}

/// <summary>
/// <see cref="IWindowLayoutService"/> implementation backed by the browser's
/// <c>localStorage</c> via <see cref="IJSRuntime"/> interop.
/// </summary>
public sealed class WindowLayoutService(IJSRuntime js) : IWindowLayoutService
{
    private const string StorageKey = "mls_window_layout";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <inheritdoc />
    public async Task SaveAsync(IEnumerable<WindowState> windows, CancellationToken ct)
    {
        var snapshot = windows.ToArray();
        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        await js.InvokeVoidAsync("localStorage.setItem", ct, StorageKey, json)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WindowState[]> LoadAsync(CancellationToken ct)
    {
        string? json;
        try
        {
            json = await js.InvokeAsync<string?>("localStorage.getItem", ct, StorageKey)
                .ConfigureAwait(false);
        }
        catch (JSException)
        {
            // JS interop unavailable during pre-render — return empty.
            return [];
        }

        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<WindowState[]>(json, _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
