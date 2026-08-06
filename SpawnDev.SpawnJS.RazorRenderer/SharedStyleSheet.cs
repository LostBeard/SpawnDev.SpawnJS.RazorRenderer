namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// A shared style sheet
/// </summary>
public class SharedStyleSheet
{
    /// <summary>
    /// The style sheet url
    /// </summary>
    public string Href
    {
        get => _Href;
        set
        {
            if (value == _Href) return;
            var oldValue = _Href;
            _Href = value;
            OnStyleSheetsChanged?.Invoke(this, oldValue);
        }
    }
    internal string _Href { get; set; }
    internal event Action<SharedStyleSheet, string> OnStyleSheetsChanged = default!;
    internal event Action<SharedStyleSheet> OnStyleSheetRemoved = default!;
    internal SharedStyleSheet(string url)
    {
        _Href = url;
    }
    /// <summary>
    /// Returns true if removed
    /// </summary>
    public bool Removed { get; private set; }
    /// <summary>
    /// Remvoed the sahred style sheet
    /// </summary>
    public void Remove()
    {
        if (Removed) return;
        Removed = true;
        OnStyleSheetRemoved?.Invoke(this);
    }
}
