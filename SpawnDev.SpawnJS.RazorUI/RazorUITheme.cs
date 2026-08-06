using SpawnDev.SpawnJS.RazorRenderer;

namespace SpawnDev.SpawnJS.RazorUI;

/// <summary>
/// RazorUI's theme service. On construction it registers RazorUI's component styles (<c>razor-ui/base.css</c>)
/// and the active theme (<c>razor-ui/theme.{name}.css</c>) as <see cref="SharedStyleSheet"/>s on the
/// <see cref="SpawnDomRenderer"/>, so the renderer loads them into every root it renders - the document for
/// light-DOM roots and each shadow root for encapsulated ones - with nothing but the app script on the host page.
/// <para>
/// Registered by <c>AddRazorUI()</c> and started as an <see cref="IBackgroundService"/>. Because every
/// background-service constructor runs before any renderer <c>Ready</c>/init, these sheets are registered
/// before the renderer attaches shared sheets to its roots. URLs resolve against
/// <see cref="SpawnJSRuntime.AppBaseUri"/>, so they are correct from the app's own origin, a CDN, or an
/// extension package.
/// </para>
/// <para><see cref="SetTheme(string)"/> repoints the theme sheet's <see cref="SharedStyleSheet.Href"/>, which
/// live-swaps the theme across every root at once (the browser caches each theme, so repeat switches are instant).</para>
/// </summary>
public sealed class RazorUITheme : IBackgroundService
{
    /// <summary>Built-in theme name (matches <c>razor-ui/theme.light.css</c>).</summary>
    public const string Light = "light";
    /// <summary>Built-in theme name (matches <c>razor-ui/theme.dark.css</c>).</summary>
    public const string Dark = "dark";

    readonly SpawnDomRenderer _renderer;
    readonly SpawnJSRuntime _js;
    string _theme = Light;

    SharedStyleSheet _baseCSS;
    SharedStyleSheet _themeCSS;

    /// <summary>The active theme name.</summary>
    public string Theme => _theme;
    /// <summary>Constructed by DI.</summary>
    public RazorUITheme(SpawnDomRenderer renderer, SpawnJSRuntime js)
    {
        _renderer = renderer;
        _js = js;
        _baseCSS = _renderer.AddSharedStyleSheet(Href("base.css"));
        _themeCSS = _renderer.AddSharedStyleSheet(Href($"theme.{_theme}.css"));
    }

    string Href(string file) => $"{_js.AppBaseUri}razor-ui/{file}";

    /// <summary>
    /// Switches the theme by name (a <c>razor-ui/theme.{name}.css</c> file). Repoints every theme link
    /// already attached, so all roots update. The browser caches each theme after first load, so repeat
    /// switches are instant.
    /// </summary>
    public void SetTheme(string name)
    {
        _theme = name;
        _themeCSS.Href = Href($"theme.{name}.css");
    }
}
