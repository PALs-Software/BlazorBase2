using BlazorBase.User.Services;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Wasm;

/// <summary>
/// The browser form factor. A WASM app is always served from the same origin it talks to,
/// so components such as <c>UserPreferencesPanel</c> use this to hide the server URL input
/// that only the MAUI hosts need.
/// </summary>
public class WasmFormFactor : IFormFactor
{
    public string GetFormFactor() => "Web";

    public string GetPlatform() => "WebAssembly";
}
