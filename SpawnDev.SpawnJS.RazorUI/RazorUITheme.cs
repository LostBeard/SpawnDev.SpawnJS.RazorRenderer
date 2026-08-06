using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorRenderer;

namespace SpawnDev.SpawnJS.RazorUI;

/// <summary>
/// Loads RazorUI's stylesheets into the roots your components live in - by CODE, so the only thing that has
/// to be on the host page is the app script. Register with <c>AddRazorUI()</c> and inject it; after mounting
/// a component into a shadow root (or the document), call <see cref="ApplyTo(ShadowRoot)"/>.
/// <para>
/// Styles are static files under <c>wwwroot/razor-ui/</c> (served at <c>/razor-ui/</c> thanks to
/// <c>StaticWebAssetBasePath</c>, so there is no <c>_content/</c> path to trip up a browser extension). They
/// are attached as <c>&lt;link&gt;</c> elements resolved against <see cref="SpawnJSRuntime.AppBaseUri"/>, so
/// the URLs are correct whether the app runs from its own origin, a CDN, or an extension package.
/// </para>
/// </summary>
public sealed class RazorUITheme
{
    /// <summary>Built-in theme name (matches <c>razor-ui/theme.light.css</c>).</summary>
    public const string Light = "light";
    /// <summary>Built-in theme name (matches <c>razor-ui/theme.dark.css</c>).</summary>
    public const string Dark = "dark";

    readonly SpawnDomRenderer _renderer;
    readonly SpawnJSRuntime _js;
    readonly List<Element> _themeLinks = new();
    string _theme = Light;

    /// <summary>Constructed by DI.</summary>
    public RazorUITheme(SpawnDomRenderer renderer, SpawnJSRuntime js)
    {
        _renderer = renderer;
        _js = js;
    }

    /// <summary>The active theme name.</summary>
    public string Theme => _theme;

    string Href(string file) => $"{_js.AppBaseUri}razor-ui/{file}";

    /// <summary>Loads the component styles + current theme into a shadow root (styling everything inside it).</summary>
    public void ApplyTo(ShadowRoot root)
    {
        _renderer.AttachStyleSheet(root, Href("base.css"));
        _themeLinks.Add(_renderer.AttachStyleSheet(root, Href($"theme.{_theme}.css")));
    }

    /// <summary>Loads the component styles + current theme into the document (light-DOM usage).</summary>
    public void ApplyTo(Document document)
    {
        _renderer.AttachStyleSheet(document, Href("base.css"));
        _themeLinks.Add(_renderer.AttachStyleSheet(document, Href($"theme.{_theme}.css")));
    }

    /// <summary>
    /// Switches the theme by name (a <c>razor-ui/theme.{name}.css</c> file). Repoints every theme link
    /// already attached, so all roots update. The browser caches each theme after first load, so repeat
    /// switches are instant.
    /// </summary>
    public void SetTheme(string name)
    {
        _theme = name;
        var href = Href($"theme.{name}.css");
        foreach (var link in _themeLinks) link.SetAttribute("href", href);
    }
}
