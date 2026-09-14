using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// A node in the renderer's <em>logical</em> tree.
/// <para>
/// Blazor's browser renderer overloads real DOM nodes with hidden symbol properties
/// (<c>logicalChildren</c>/<c>logicalParent</c>) so a DOM node <em>is</em> a logical element. SpawnJS
/// wrappers can't carry arbitrary JS symbols cheaply, so instead each logical element is a small C#
/// wrapper that owns its physical <see cref="Node"/> plus the logical parent/child bookkeeping.
/// </para>
/// <para>
/// A logical element's <see cref="Node"/> is one of: an <see cref="Element"/> (a real element), a
/// <see cref="Text"/> node, or a <see cref="Comment"/> that acts as a component/markup <em>container</em>
/// whose logical children are physically its following siblings (never its DOM descendants). The root
/// logical element wraps the host <see cref="Element"/> or <see cref="ShadowRoot"/>.
/// </para>
/// </summary>
internal sealed class LogicalElement
{
    /// <summary>The physical DOM node backing this logical element.</summary>
    public Node Node = default!;

    /// <summary>The logical parent, or null for a root.</summary>
    public LogicalElement? Parent;

    /// <summary>
    /// The namespace this node's CHILDREN must be created in.
    /// <para>
    /// ⚠️ This is deliberately a statement about children, not about this node. They differ on exactly one
    /// element: a <c>&lt;foreignObject&gt;</c> is itself in the SVG namespace but its content is HTML again,
    /// so it is created with <c>createElementNS</c> and still carries <see cref="ElementNamespace.Html"/>.
    /// Blazor asks the same questions of the DOM
    /// (<c>closest.namespaceURI === SVG &amp;&amp; closest.tagName !== 'foreignObject'</c>, and
    /// <c>closest.namespaceURI === MathML</c>); carrying the answer is the same predicate without the two
    /// property reads per element.
    /// </para>
    /// </summary>
    /// <remarks>
    /// 🔴 CARRIED, NOT LOOKED UP. It used to be asked of the DOM at creation time
    /// (<c>closest.NamespaceURI == SvgNamespace</c>), and that answered wrong: every child of an
    /// <c>&lt;svg&gt;</c> was created as <c>http://www.w3.org/1999/xhtml</c>. An HTML element named
    /// "ellipse" is perfectly legal, inherits the CSS aimed at it, and reports zero geometry - so the
    /// markup and the styles look right in devtools and NOTHING is drawn. Captain, on the avatar:
    /// "I can use devtools to see the svg there.. but nothing is actually visible. Just an empty
    /// borderless box", and it had been that way since the avatars were written.
    /// <para>
    /// ⚠️ The render tree already knows the answer - the parent chain is being built as we go - so
    /// inheriting the flag is both correct and cheaper than the two JS property reads per element the
    /// lookup cost.
    /// </para>
    /// <para>
    /// MathML joined this later and rides the identical mechanism; the only asymmetry is that SVG has the
    /// <c>&lt;foreignObject&gt;</c> exemption and MathML has none.
    /// </para>
    /// </remarks>
    public ElementNamespace ChildNamespace;

    /// <summary>
    /// Ordered logical children. For an <see cref="Element"/> node these are its physical descendants;
    /// for a <see cref="Comment"/> container they are its physical following siblings.
    /// </summary>
    public readonly List<LogicalElement> Children = new();

    /// <summary>
    /// Live DOM event listeners registered on this element, keyed by DOM event name (e.g. "click").
    /// Only ever populated for element nodes. The renderer owns these <see cref="Callback"/>s and disposes
    /// them when the element leaves the tree (unlike Blazor's global event delegator, which relies on GC).
    /// </summary>
    public Dictionary<string, EventListenerRegistration>? EventListeners;

    /// <summary>The captured <c>@ref</c> id for this element, if any.</summary>
    public string? ElementReferenceCaptureId;

    /// <summary>True when the backing node can directly parent DOM children (element or document fragment).</summary>
    public bool IsDomContainer => Node is Element || Node is DocumentFragment;
}

/// <summary>A single live DOM event listener the renderer created and must dispose.</summary>
internal sealed class EventListenerRegistration
{
    public required Callback Callback;
    public required string EventName;
    /// <summary>
    /// The Blazor event-handler id currently bound to this listener. Re-renders update this in place so a
    /// changed handler reuses the one live DOM listener rather than churning add/remove.
    /// </summary>
    public ulong HandlerId;
}
