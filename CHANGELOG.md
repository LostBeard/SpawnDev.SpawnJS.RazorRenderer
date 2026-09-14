# Changelog

All notable changes to SpawnDev.SpawnJS.RazorRenderer and SpawnDev.SpawnJS.RazorUI.

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
