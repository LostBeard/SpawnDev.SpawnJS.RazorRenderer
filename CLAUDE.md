# SpawnDev.SpawnJS.RazorRenderer - Project Rules

Global rules: `D:\users\tj\Projects\CLAUDE.md`. SpawnJS core rules: `SpawnDev.SpawnJS\SpawnDev.SpawnJS\CLAUDE.md`. Read both first.

**What it is:** interactive Blazor components in .NET WebAssembly **without the Blazor JS runtime.** Razor components render straight to the real DOM through SpawnJS. `@onclick`, `@bind`, `StateHasChanged`, component lifecycle and `EventCallback` all work, and components mount into any element or **shadow root** - so a real interactive Blazor component can be dropped onto any page.

## Layout

Repo root is the inner `SpawnDev.SpawnJS.RazorRenderer\SpawnDev.SpawnJS.RazorRenderer\`.

| | |
|---|---|
| `SpawnDev.SpawnJS.RazorRenderer` | the renderer |
| `SpawnDev.SpawnJS.RazorUI` | UI component library (published separately) |
| `RazorRendererDemo` | demo app |
| `WasmTestHost` / `TestRunner` / `TestsShared` | the test harness |

Publish bats are in the OUTER folder, and there are TWO sets - `_publish-nuget*.bat` (renderer) and `_publish-ui-nuget*.bat` (RazorUI). `PLANS.md` and `CHANGELOG.md` are at the repo root. `_OLD_` and `_OLD2_` in the outer folder are dead - do not read them for current behaviour.

## 🔴 Verify the SIDE EFFECT, not the widget's visual state

Checkbox `@onchange` does NOT fire reliably on a programmatic `.click()` - the native `checked` property flips, which masks the failure, while the C# handler never runs and nothing is saved.

Use an `@onclick` toggle BUTTON (SettingsApp's own pattern). When testing any control, assert the thing that was supposed to CHANGE, never that the widget looks right.

## ⭐ A parent re-render skips children whose parameters did not change

Blazor's diff will not re-render a child component when its parameters are unchanged, so pushing new state into a parent is not enough. Have the child **subscribe** to the state source and call `StateHasChanged` itself.

## ⭐ Smooth dragging: native pointer events + GPU transform

Do not route a drag through Blazor's event pipeline and re-render per move. Subscribe to native `pointermove` and move the element with a `translate3d` transform. The renderer stays out of the loop and the drag runs at compositor speed.

## Testing

`TestRunner` is a Playwright harness over `WasmTestHost`; shared cases live in `TestsShared`.

🔴 **Navigate on `DOMContentLoaded`, NOT `NetworkIdle`.** A SharedWorker or persistent-connection test never lets the network go idle, so `GotoAsync` throws at the 60s nav timeout BEFORE the harness reads the page's `RESULTS:` done-signal. This masked a real fix once - the harness kept "failing" for an entirely unrelated reason. Rely on the in-page `RESULTS:` console line for completion.

⚠️ The test-type list is EXPLICIT - a new test class that is not added to it silently does not run and the `Ran:` count stays put. Check the count moved.

⚠️ Verify local changes via the **Debug** build (project references). Release pulls the PUBLISHED NuGet package, so a Release run tests published bits, not your edits. Bump `-local.N` when iterating; NuGet caches by version.

## Publishing

Two packages ship from this repo - bump and ship BOTH when a renderer fix affects RazorUI, since RazorUI pins the renderer and consumers get it transitively at the pinned version. nuget.org pushes need Captain's per-push sign-off.
