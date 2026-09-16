using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// A typed <c>@ref</c> target: write <c>@ref="_canvas"</c> against an
/// <c>ElementRef&lt;HTMLCanvasElement&gt;</c> field and ask it for the element when you need one.
/// </summary>
/// <example>
/// <code>
/// &lt;canvas @ref="_canvas"&gt;&lt;/canvas&gt;
/// ...
/// ElementRef&lt;HTMLCanvasElement&gt; _canvas;
/// using var canvas = _canvas.Get();      // null until the first render has captured it
/// </code>
/// </example>
/// <remarks>
/// <para>
/// 🔴 WHY A HOLDER AND NOT AN IMPLICIT OPERATOR ON THE WRAPPERS. A user-defined conversion has to be
/// declared in either the source type or the destination type. The source is Microsoft's
/// <see cref="ElementReference"/>, and the destination would be <c>HTMLCanvasElement</c> and friends -
/// which live in <c>SpawnDev.SpawnJS</c>, a package that deliberately does not reference
/// <c>Microsoft.AspNetCore.Components</c> and therefore cannot name <see cref="ElementReference"/> at all.
/// Per-type operators would mean putting a Blazor dependency into the dependency-free core. This package
/// already references Components, so ONE generic type declared here solves it for every wrapper.
/// </para>
/// <para>
/// ⭐ IT RESOLVES LAZILY, WHICH IS THE POINT. Converting at capture time would allocate a live JS slot on
/// every capture, and <c>@ref</c> re-captures on re-render - SpawnJS slots are manual, nothing collects
/// them, and the <c>@ref</c> syntax gives a component nowhere to dispose the previous value. So this
/// stores only the <see cref="ElementReference"/> (a free struct) and hands out a wrapper when asked, for
/// the caller to dispose with <c>using</c>, exactly as <c>As&lt;T&gt;()</c> does.
/// </para>
/// <para>
/// ⚠️ <see cref="Get"/> returns null until the reference has been captured - a <c>@ref</c> field is not
/// populated until after the first render - and after the element leaves the tree. Both are ordinary
/// states, not faults.
/// </para>
/// </remarks>
/// <typeparam name="T">The wrapper type to hand out, e.g. <c>HTMLCanvasElement</c>.</typeparam>
public readonly struct ElementRef<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>
    where T : SpawnJSObject
{
    /// <summary>The captured reference. Default until the first render assigns it.</summary>
    public ElementReference Reference { get; }

    /// <summary>Wraps <paramref name="reference"/>. The Razor compiler's assignment uses the implicit form.</summary>
    public ElementRef(ElementReference reference) => Reference = reference;

    /// <summary>
    /// What makes <c>@ref</c> accept this type: the generated capture assigns an
    /// <see cref="ElementReference"/> to the field, so the conversion must be implicit.
    /// </summary>
    public static implicit operator ElementRef<T>(ElementReference reference) => new(reference);

    /// <summary>The captured reference, for APIs that take one.</summary>
    public static implicit operator ElementReference(ElementRef<T> elementRef) => elementRef.Reference;

    /// <summary>True once the element has been captured.</summary>
    public bool IsCaptured => !string.IsNullOrEmpty(Reference.Id);

    /// <summary>
    /// The live element, or <see langword="null"/> if it has not been captured or has left the tree.
    /// </summary>
    /// <remarks>⚠️ A fresh JS reference each call - the caller owns it and should <c>using</c> it.</remarks>
    public T? Get() => Reference.AsElement<T>();
}
