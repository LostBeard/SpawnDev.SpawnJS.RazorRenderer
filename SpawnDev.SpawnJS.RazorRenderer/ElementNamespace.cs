namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// The XML namespace an element is created in. HTML documents host three, and which one an element lands
/// in is decided by its ancestors, not by its tag name alone - <c>&lt;title&gt;</c> is a legal element in
/// all three and means something different in each.
/// </summary>
/// <remarks>
/// 🔴 GETTING THIS WRONG IS SILENT. An element created in the wrong namespace still has the right tag
/// name, still matches a CSS type selector (which matches the LOCAL NAME), and still inherits the styles
/// aimed at it - it simply has no layout behaviour, so it draws nothing and no error is raised anywhere.
/// See <see cref="LogicalElement.ChildNamespace"/>.
/// </remarks>
internal enum ElementNamespace
{
    /// <summary><c>http://www.w3.org/1999/xhtml</c> - created with <c>createElement</c>.</summary>
    Html = 0,

    /// <summary><c>http://www.w3.org/2000/svg</c>, opened by <c>&lt;svg&gt;</c>.</summary>
    Svg,

    /// <summary><c>http://www.w3.org/1998/Math/MathML</c>, opened by <c>&lt;math&gt;</c>.</summary>
    MathML,
}
