using Microsoft.AspNetCore.Components;

namespace SpawnDev.SpawnJS.RazorUI;

/// <summary>
/// Base for RazorUI components. Captures unmatched attributes (<c>id</c>, <c>data-*</c>, <c>aria-*</c>,
/// <c>class</c>, <c>style</c>, ...) for passthrough onto the root element, and MERGES a caller-supplied
/// <c>class</c>/<c>style</c> with the component's own rather than letting the splat clobber them.
/// <para>
/// Components render <c>class="@CssClass("ui-x")"</c> and <c>@attributes="PassThroughAttributes"</c>; the
/// latter has <c>class</c>/<c>style</c> stripped out (they are merged explicitly) so nothing renders twice.
/// </para>
/// </summary>
public abstract class UiComponentBase : ComponentBase
{
    /// <summary>Caller-set attributes that are not declared parameters - forwarded to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    /// <summary>Combines the component's own <paramref name="baseClasses"/> with any caller-supplied <c>class</c>.</summary>
    protected string CssClass(string baseClasses)
    {
        var extra = Get("class");
        return string.IsNullOrEmpty(extra) ? baseClasses : baseClasses + " " + extra;
    }

    /// <summary>Combines the component's own <paramref name="baseStyle"/> with any caller-supplied <c>style</c>.</summary>
    protected string? Style(string? baseStyle)
    {
        var extra = Get("style");
        if (string.IsNullOrEmpty(extra)) return baseStyle;
        if (string.IsNullOrEmpty(baseStyle)) return extra;
        return baseStyle!.TrimEnd(';') + ";" + extra;
    }

    /// <summary>The splat attributes with <c>class</c>/<c>style</c> removed (they are merged explicitly).</summary>
    protected IReadOnlyDictionary<string, object>? PassThroughAttributes
    {
        get
        {
            if (Attributes is null) return null;
            if (!Attributes.ContainsKey("class") && !Attributes.ContainsKey("style")) return Attributes;
            var d = new Dictionary<string, object>(Attributes.Count);
            foreach (var kv in Attributes)
                if (kv.Key != "class" && kv.Key != "style") d[kv.Key] = kv.Value;
            return d;
        }
    }

    string? Get(string key) => Attributes is not null && Attributes.TryGetValue(key, out var v) ? v as string : null;
}
