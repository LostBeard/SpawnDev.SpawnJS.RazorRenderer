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

- Elements, text, attributes, nested components, regions, keyed reorders, markup blocks (`MarkupString`)
- `@onclick` and the mouse/keyboard/focus events; `@bind` / `@bind:event`
- `StateHasChanged` re-render, component lifecycle, parameter flow into child components
- `EventCallback`s and typed `EventArgs` (`MouseEventArgs`, `KeyboardEventArgs`, `ChangeEventArgs`, ...)
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
