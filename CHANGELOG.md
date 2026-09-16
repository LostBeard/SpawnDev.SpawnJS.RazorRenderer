# Changelog

All notable changes to SpawnDev.SpawnJS.RazorRenderer and SpawnDev.SpawnJS.RazorUI.

## RazorRenderer 2.1.11 - 2026-09-16

### Added

- **`ElementRef<T>` - a typed `@ref` target.** Write `@ref="_canvas"` against an
  `ElementRef<HTMLCanvasElement>` field and ask it for the element: `using var canvas = _canvas.Get();`.
  The type says what the element IS, so nothing downstream has to name the wrapper again or get it wrong.

  🔴 **Why a holder rather than implicit operators on the wrappers.** A user-defined conversion must be
  declared in the source type or the destination type. The source is Microsoft's `ElementReference`; the
  destinations would be `HTMLCanvasElement` and friends, which live in `SpawnDev.SpawnJS` - a package that
  deliberately does not reference `Microsoft.AspNetCore.Components` and so cannot name `ElementReference`
  at all. Per-type operators would mean pushing a Blazor dependency into the dependency-free core. This
  package already references Components, so one generic type declared here covers every wrapper.

  ⭐ **It resolves lazily, and that is the point.** Converting at capture time would allocate a live JS
  slot on every capture, and `@ref` re-captures on re-render - SpawnJS slots are manual, nothing collects
  them, and the `@ref` syntax gives a component nowhere to dispose the previous value. `ElementRef<T>`
  stores only the reference (a free struct) and hands out a wrapper when asked, for the caller to
  `using`.

  ⚠️ `Get()` returns null before the first render has captured the element, and after it leaves the tree.
  Both are ordinary states.

## RazorRenderer 2.1.10 - 2026-09-16

### Fixed

- **`@ref` could not be resolved with `ElementReference.As<T>()`, and failed silently.** That extension
  ships in `SpawnDev.SpawnJS.Blazor` and resolves through `ElementReference.Context`, which it only
  recognises as Blazor's `WebElementReferenceContext`. Under `SpawnDomRenderer` it is not, so the method
  returned `null!` - a null-forgiving null from a signature that promises a value - and every caller got a
  bare `NullReferenceException` naming nothing.

  MEASURED 2026-09-16 in SpawnDev.AI: a chat transcript that would not scroll. The cause was blamed in
  turn on flex layout, on scroll-anchoring logic and on image load order, through three rounds of fixes to
  a method that had never once run, because the element lookup at the top of it silently produced nothing.

  `SpawnDomRenderer` now sets `ElementReferenceContext` to a `SpawnDomElementReferenceContext` carrying
  itself, so every `@ref` it captures knows which renderer can resolve it.

### Added

- **`ElementReference.As<T>()` / `.AsElement<T>()`** in the `SpawnDev.SpawnJS.RazorRenderer` namespace,
  resolving through that context. Identical in effect to `SpawnDomRenderer.GetElement<T>()`, which remains
  the explicit form; this is for components that do not already have the renderer to hand.

  ⚠️ **Do not reference `SpawnDev.SpawnJS.Blazor` from a `SpawnDomRenderer` app.** Two equally-applicable
  extensions with the same signature is a CS0121 ambiguity, not a silent pick. That is the correct
  outcome and costs nothing: that package exposes only this extension and
  `SpawnJSRunAsync(this WebAssemblyHost)`, neither of which a RazorRenderer app uses.

  ⚠️ It returns `T?` deliberately. A `@ref` field is not populated until after the first render, so null
  is a real answer and callers must handle it - the Blazor version's `T`-that-is-sometimes-null is exactly
  what made this expensive.

  ⭐ No global. Each reference carries its own renderer, so an element captured by one app resolves
  against that app even when two .NET WASM apps share a page - which is the scenario a static
  `Instance` would break silently.

## RazorRenderer 2.1.7 - 2026-09-13

### Fixed

- **Static SVG content rendered in the HTML namespace and drew nothing.** The Razor compiler coalesces
  every fully static element run into a single `Markup` frame, and `InsertMarkup` parsed all of them
  through an HTML `<template>`, which parses in the HTML namespace. In the common component shape -
  dynamic attributes on the `<svg>`, static geometry inside it - the `<svg>` was namespaced correctly
  while every `<polygon>`/`<circle>`/`<path>`/`<ellipse>` child became an `HTMLUnknownElement` carrying
  the right tag name and matching the right CSS, reporting zero geometry and painting nothing. Markup
  destined for an SVG subtree now parses in an `svg:g` context element so the fragment parser runs in
  foreign-content mode, matching Blazor's own `BrowserRenderer`. This is the half of the namespace fix
  that the 2.1.6 element-frame change did not cover, and it is what left `UiIcon` and consumer SVG
  (avatars) invisible after that release. Guards:
  `SvgNamespaceTests.StaticMarkupChildOfSvgIsInSvgNamespaceTest`,
  `SvgNamespaceTests.UiIconGeometryIsInSvgNamespaceTest`.
- **`<foreignObject>` itself was created in the HTML namespace.** It was excluded from the SVG namespace
  at its own creation, but `<foreignObject>` stops namespace propagation to its *content* - the element
  is an SVG element. Blazor's rule tests the parent
  (`closest.namespaceURI === SVG && closest.tagName !== 'foreignObject'`); `LogicalElement.IsSvg` now
  means "children of this node are SVG", so a `<foreignObject>` is created with `createElementNS` and
  still carries `false`. An XHTML `foreignObject` lays out and clips nothing. Guard:
  `SvgNamespaceTests.ForeignObjectReEntersHtmlNamespaceTest`.

### Added

- **MathML namespace support.** `<math>` now opens the MathML namespace exactly as `<svg>` opens SVG, on
  both paths - `createElementNS` for element frames, and a `mathml:mrow` parse context for markup frames.
  Previously every MathML element was an `HTMLUnknownElement`, which renders as unstyled inline text
  instead of maths: present, wrong, and silent. `LogicalElement.IsSvg` (bool) became
  `LogicalElement.ChildNamespace` (`ElementNamespace` - Html/Svg/MathML); both types are `internal`, so
  this is not a public API change. Branch order matches Blazor's - SVG is tested before MathML, so an
  `<svg>` nested inside a `<math>` re-opens SVG for itself and its children.
  ⚠️ MathML gets no `<foreignObject>`-style exemption, matching Blazor, whose `isMathMLElement()` tests
  only the namespace. The real MathML escape into HTML is `<annotation-xml encoding="text/html">`, which
  neither renderer implements. Guards: `MathMLNamespaceTests` (4 cases).

### Tests

- Added `SvgNamespaceTests` (5 cases) asserting `namespaceURI` across all three ways a node reaches an
  `<svg>`: element frame, markup frame, and `<foreignObject>` content. 🔴 The previous SVG assertion
  (`querySelector(".ui-icon polygon")`, labelled "SVG child namespace working") could not fail - a CSS
  type selector matches an element's **local name**, so it matched an `HTMLUnknownElement` named
  `polygon` exactly as happily as a real `SVGPolygonElement`, and passed for the entire time the defect
  was live. Suite 34 -> 39.

## RazorRenderer 1.0.7 - 2026-08-09

### Added

- **`SpawnDomRenderer.OnAfterRender(bool firstRender)` / `OnAfterRenderAsync(bool firstRender)` events.**
  Raised from `UpdateDisplayAsync` after each render batch is applied to the DOM (`firstRender` true only
  for the first batch). Mirrors `ComponentBase.OnAfterRender` at the renderer level so a background
  **service** (which is not a component) can react to rendering - e.g. do one-time work once the UI exists,
  without blocking `Ready`. Handlers run on the renderer `Dispatcher` (safe to touch component state); the
  async event is fire-and-forget with faults routed to the renderer's exception handler, so a long-running
  handler never blocks the render pipeline. Additive, binary-compatible.

### Tests

- Added `ParentChildReRenderTests` documenting/guarding standard Blazor diff behavior faithfully reproduced
  here: a parent re-render re-renders only children whose parameters changed (a parameterless/unchanged-param
  child is skipped). Verified empirically that the skip is in the base `RenderTreeDiffBuilder`, not this
  renderer.

## RazorRenderer 1.0.5 / RazorUI 1.0.2 - 2026-08-07

### Fixed

- **Markup rendering on Trusted Types-enforcing pages (YouTube, Gmail, ...).** `SpawnDomRenderer` parsed
  Blazor `Markup` frames with `DOMParser.parseFromString`, which is a Trusted Types injection sink. On a
  host page whose CSP enforces `require-trusted-types-for 'script'` the call threw
  `This document requires 'TrustedHTML'`; the exception was swallowed by `HandleException` and the render
  batch aborted mid-mutation, leaving a half-built (visibly corrupt) shadow DOM - only some elements
  rendered, re-renders removed the wrong nodes. The renderer now lazily creates a cached Trusted Type
  policy (`spawndev-razorrenderer`) and parses markup as `TrustedHTML` where the page enforces Trusted
  Types, and as a raw string everywhere else. Uses a `Callback` (not `new Function`, which such a page's
  `unsafe-eval` block also refuses) and fails loud if a restrictive `trusted-types` CSP allowlist blocks
  the policy name. Requires SpawnDev.SpawnJS 1.1.9. Guards:
  `TrustedTypesTests.MarkupRendersThroughTrustedTypesPathTest`,
  `TrustedTypesTests.TrustedTypePolicyParsesHtmlTest`.

- RazorUI 1.0.2 is a rebuild on the fixed renderer (no API change).
