# SpawnDev.SpawnJS.RazorRenderer
[![NuGet](https://img.shields.io/nuget/dt/SpawnDev.SpawnJS.RazorRenderer.svg?label=SpawnDev.SpawnJS.RazorRenderer)](https://www.nuget.org/packages/SpawnDev.SpawnJS.RazorRenderer)  

> 💜 **Built and maintained by one independent developer** — no company, no overhead, just code. If SpawnDev.SpawnJS.RazorRenderer saves you time, please consider [**sponsoring its development »**](https://github.com/sponsors/LostBeard). Sponsorship is what keeps it alive and maintained.

Interactive Blazor components in .NET WebAssembly **without the Blazor JS runtime.**

Razor components render straight to the real DOM through [SpawnJS](https://github.com/LostBeard/SpawnDev.SpawnJS) -
`@onclick`, `@bind`, `StateHasChanged`, component lifecycle and `EventCallback`s all work - and mount into any
element or **shadow root**. That means you can drop a real, interactive Blazor component onto any page (even one
you don't control, even loaded from a CDN link) with full DOM and CSS isolation, and no Blazor circuit.

## How it is different from Blazor WebAssembly

Blazor's own renderer serializes each render batch and ships it to JavaScript, where `Web.JS` walks the edits and
mutates the DOM. `SpawnDomRenderer` is a `Microsoft.AspNetCore.Components.RenderTree.Renderer` that consumes the
same `RenderBatch` **in C#** and applies every edit to the DOM directly through SpawnJS's typed interop. The
component model is genuine Blazor - only the renderer is ours, and there is no Blazor JavaScript runtime or SignalR
circuit involved.

## Setup

```csharp
using SpawnDev.SpawnJS.RazorRenderer;

// .NET WASM app (SpawnJS host - no Blazor bootstrap)
builder.Services.AddSpawnJSRuntime(out var JS);
builder.Services.AddRazorRenderer();   // registers the SpawnDomRenderer singleton
```

Mount a component into any element:

```csharp
var renderer = app.Services.GetRequiredService<SpawnDomRenderer>();
using var document = JS.Get<Document>("document");

var host = document.CreateElement<HTMLDivElement>("div");
document.Body!.Append(host);

await renderer.RenderComponentAsync<App>(host);           // App is your root .razor component
// with parameters:
await renderer.RenderComponentAsync<App>(host, new() { ["Title"] = "Hello" });
```

One renderer hosts many root components; each mount gets its own component id.

## Shadow root (DOM + CSS isolation)

`RenderComponentAsync` takes any `Node`, and `ShadowRoot` is a `Node`, so mounting into a shadow root gives you
full encapsulation from the host page for free - host-page CSS cannot reach in, your component's CSS cannot leak
out, and events fire correctly across the boundary (listeners are attached per element, so there is no
shadow-retargeting problem a document-level delegator would have).

```csharp
var host = document.CreateElement<HTMLDivElement>("div");
document.Body!.Append(host);
var shadow = host.AttachShadow(new AttachShadowRootOptions { Mode = "open" }); // or "closed"
await renderer.RenderComponentAsync<App>(shadow);
```

## Element references (`@ref`)

Capture an element with `@ref` and resolve it to a live SpawnJS wrapper through the renderer - with **no**
`document.getElementById`/`querySelector`, so it works identically in the light DOM and **inside a shadow root**
(where a document query cannot reach):

```razor
@inject SpawnDomRenderer SpawnDomRenderer

<video @ref="_videoRef" controls></video>

@code {
    ElementReference _videoRef;
    HTMLVideoElement? _video;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _video = SpawnDomRenderer.GetElement<HTMLVideoElement>(_videoRef);
            _video!.Muted = true;
            _ = _video.Play();
        }
    }
}
```

The framework populates the `ElementReference` field before `OnAfterRender` runs; the renderer then resolves it
from the node it **already holds** in its logical tree - which is why no document query is involved and the
lookup crosses the shadow boundary. `GetElement<T>(ElementReference)` returns a fresh, owned wrapper (dispose it
when done); `GetElementNode(ElementReference)` returns the raw `Node`.

## Stable element identity in lists (`@key`)

Blazor's diff is positional by default: remove a **middle** item from a keyless list and the diff mutates the
surviving elements in place and drops the **last** DOM node - so anything you attached to a specific element (a
captured `@ref`, a `<video>`, a canvas context) ends up on the wrong item. Add `@key` and the diff performs a
real removal/permutation instead, giving a **1:1 lifetime between each component / data item and its physical
element**:

```razor
@foreach (var item in items)
{
    <div @key="item.Id">@item.Name</div>
}
```

`SpawnDomRenderer` honors `@key` faithfully - this is verified: removing the middle of a 5-item keyed list
removes exactly that item's node and leaves every survivor's **original** DOM node in place (checked with a JS
expando the diff cannot touch). No "hide it with `display:none` until it reaches the end of the list, then
remove it" workaround is needed to keep an element bound to its data.

## Where this shines

- **Web Components / custom elements** - attach a shadow root in your element's `connectedCallback` and hand it
  to `RenderComponentAsync`; the element becomes a real interactive Blazor component, fully encapsulated, and
  `@ref` wires up its internals without ever touching the host document.
- **Browser extensions / content scripts** - render your UI into a (preferably `closed`) shadow root beside a
  page you do not control. The page's CSS cannot bleed in, your markup cannot leak out, and because element
  references resolve through the renderer instead of `document`, the host page's ids and scripts cannot collide
  with or interfere with yours.

## ⚠️ Requirement: the host project must import the Web directive namespace

`@onclick`, `@bind` and the other event directives are only recognized when
`Microsoft.AspNetCore.Components.Web` is in the Razor `@using` scope. If it is missing, the Razor compiler emits
them as **literal attributes** (`@onclick="..."` ends up as a real DOM attribute) and **events silently do
nothing** - the app compiles cleanly, boots, and renders. Make sure the project hosting your components has an
`_Imports.razor` containing at least:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
```

## What works today

- Elements, text, attributes, nested components, regions, markup blocks (`MarkupString`)
- `@onclick` and the mouse/keyboard/focus events; `@bind` / `@bind:event`
- `StateHasChanged` re-render, component lifecycle, parameter flow into child components
- `EventCallback`s and typed `EventArgs` (`MouseEventArgs`, `KeyboardEventArgs`, `ChangeEventArgs`, ...)
- `@ref` / `ElementReference` resolved to a live SpawnJS wrapper, shadow-root safe (no document query)
- `@key` reorders and removals with a verified 1:1 component-to-element lifetime
- Mounting into a plain element or an open/closed shadow root

Not yet ported from Blazor's renderer (contributions welcome): the full special-property set beyond `<input>`
`value`/`checked` (select/textarea/option), MathML namespace, `<option>` deferred-value ordering, cross-render
active-element focus preservation, `<script>` execution inside markup, and event delegation (listeners are
currently per element, which is correct but not yet optimized).

## Requirements

- .NET 10 WebAssembly
- [SpawnDev.SpawnJS](https://www.nuget.org/packages/SpawnDev.SpawnJS)
- `Microsoft.AspNetCore.Components.Web` (referenced transitively; the Blazor SDK is used only to compile `.razor`)

## The SpawnDev Crew

- **LostBeard** (Todd Tanner) - Captain, library author, keeper of the vision
- **Riker** - First Officer, implementation lead on consuming projects
- **Data** - Operations Officer, deep-library work, test rigor, root-cause analysis
- **Tuvok** - Security/Research Officer, design planning, documentation, code review
- **Geordi** - Chief Engineer, library internals, GPU kernels, backend work
- **Seven** - Wasm backend, GPU kernels, fail-loud verification

## License

MIT
