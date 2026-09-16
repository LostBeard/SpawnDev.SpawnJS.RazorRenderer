using Microsoft.AspNetCore.Components;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// Marks an <see cref="ElementReference"/> as having been captured by a <see cref="SpawnDomRenderer"/>,
/// and carries the renderer that can resolve it.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 THIS IS WHAT MAKES <c>@ref</c> RESOLVE WITHOUT A GLOBAL. Blazor's own
/// <c>ElementReference.As&lt;T&gt;()</c> works by asking whether <see cref="ElementReference.Context"/> is a
/// <c>WebElementReferenceContext</c>; under this renderer it is not, so that extension returned
/// <see langword="null"/> and every caller got a bare <c>NullReferenceException</c>. Giving this renderer
/// its own context type makes the same question answerable here, symmetrically.
/// </para>
/// <para>
/// ⚠️ THE ALTERNATIVE WAS A STATIC, and it would have been wrong. SpawnJS is instance-based by design -
/// two .NET WASM apps can share one page - so a <c>SpawnDomRenderer.Instance</c> global breaks silently the
/// moment a second app loads. Every reference carrying its OWN renderer has no such failure mode: an
/// element captured by app A resolves against app A even while app B is running.
/// </para>
/// </remarks>
/// <param name="renderer">The renderer that captured the reference and can resolve it.</param>
public sealed class SpawnDomElementReferenceContext(SpawnDomRenderer renderer) : ElementReferenceContext
{
    /// <summary>The renderer that captured the reference.</summary>
    public SpawnDomRenderer Renderer { get; } = renderer;
}
