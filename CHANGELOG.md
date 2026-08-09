# Changelog

All notable changes to SpawnDev.SpawnJS.RazorRenderer and SpawnDev.SpawnJS.RazorUI.

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
