using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// Makes <c>@ref</c> usable the same way under <see cref="SpawnDomRenderer"/> as it is under Blazor.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 WHY THIS EXISTS. <c>SpawnDev.SpawnJS.Blazor</c> ships an <c>ElementReference.As&lt;T&gt;()</c> that
/// resolves through <see cref="ElementReference.Context"/>, and it only recognises Blazor's own
/// <c>WebElementReferenceContext</c>. Under this renderer the context is a
/// <see cref="SpawnDomElementReferenceContext"/>, so that extension returned <see langword="null"/> - and
/// because its signature promises a value, every caller got a bare <c>NullReferenceException</c> with no
/// frame and no cause. MEASURED 2026-09-16: a chat transcript that would not scroll, blamed in turn on
/// flex layout, on scroll-anchoring logic and on image load order, through three rounds of fixes to a
/// method that had never once run.
/// </para>
/// <para>
/// ⚠️ ONE OF THESE, NOT BOTH. Two equally-applicable extensions with the same signature is a CS0121
/// ambiguity, not a silent pick - so an app that references BOTH <c>SpawnDev.SpawnJS.Blazor</c> and this
/// package and imports both namespaces will not compile. That is the correct outcome and costs nothing in
/// practice: an app hosted by <see cref="SpawnDomRenderer"/> has no use for the Blazor package, which
/// exposes only this extension and <c>SpawnJSRunAsync(this WebAssemblyHost)</c>.
/// </para>
/// <para>
/// ⭐ <see cref="SpawnDomRenderer.GetElement{T}"/> remains the explicit form and is identical in effect.
/// This is sugar for the common case where the component does not already have the renderer to hand.
/// </para>
/// </remarks>
public static class ElementReferenceExtensions
{
    extension(ElementReference elementReference)
    {
        /// <summary>
        /// Resolve a <c>@ref</c> captured by <see cref="SpawnDomRenderer"/> to its live SpawnJS element.
        /// </summary>
        /// <typeparam name="T">The wrapper type to return, e.g. <c>HTMLElement</c>.</typeparam>
        /// <returns>
        /// The element, or <see langword="null"/> if the reference is default, was captured by a different
        /// renderer, or its element has left the tree.
        /// </returns>
        /// <remarks>
        /// ⚠️ NULL IS A REAL ANSWER HERE, and a common one: a component's <c>@ref</c> fields are not
        /// populated until after the first render, so anything running earlier legitimately gets nothing.
        /// Callers must handle it rather than assume - which is why this returns <c>T?</c> and the Blazor
        /// version's <c>T</c>-that-is-sometimes-null was the trap it was.
        /// </remarks>
        public T? AsElement<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : SpawnJSObject
            => elementReference.Context is SpawnDomElementReferenceContext ctx
                ? ctx.Renderer.GetElement<T>(elementReference)
                : null;

        /// <inheritdoc cref="AsElement{T}"/>
        public T? As<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : SpawnJSObject
            => elementReference.AsElement<T>();
    }
}
