# SpawnDev.SpawnJS.RazorUI - Plan & Roadmap

> Living plan. Update the **Status** table as work lands so a cold start can pick up exactly where we left off:
> what's done, what's in flight, what's next. Every component ships with a passing test (Rule 5) and, once it
> looks right, a slot in the demo showroom for TJ to confirm.

## Vision

A polished, professional Blazor UI component library that renders through **SpawnDomRenderer** (no Blazor JS
runtime), shadow-root-first, themeable via CSS custom properties, and swappable live. Target quality bar:
Android-grade polish (memory `user_spawnwear_android_quality_ui`). Inspiration: **Radzen Blazor** (TJ's
favorite - 145+ components: DataGrid, Charts, ProgressBar/Circular, Tree, Notification, etc.). This library is
the UI foundation for most future SpawnJS apps.

### Headline use cases driving the roadmap (TJ)
1. **Custom video & audio players** - his recurring build; needs sliders, buttons/icons, time labels, progress.
2. **File browser / explorer** - has prior ones in `Projects\SpawnDev` (spawndev.com) and others; needs Tree,
   list/grid, virtualization, context menu, breadcrumb, icons.
3. **Virtualized DataGrid** - Radzen-style: async range loading (`LoadData` over a window) so thousands+ of rows
   don't all load/process at once. Virtualization is a first-class requirement, not an add-on.
4. **Full apps** - dialogs, forms, navigation, notifications, layout.

## Design principles (non-negotiable)
- **Shadow-root-first.** Components styled by `wwwroot/razor-ui/base.css` loaded (by code) into each root; theme
  tokens on `:host,:root`. `:root` alone does NOT match inside a shadow root - always `:host,:root`.
- **Theme tokens with fallbacks.** Every `var(--ui-x, fallback)` has a sane fallback so a component is usable with
  no theme loaded. New tokens are added to BOTH `theme.light.css` and `theme.dark.css`.
- **Zero raw JS / zero document queries.** DOM via SpawnJS typed wrappers; element access via `@ref` through the
  renderer (shadow-safe). SVG is fine (renderer tracks the SVG namespace).
- **Attribute splat + class/style merge** via `UiComponentBase` (`CssClass`/`Style`/`PassThroughAttributes`).
- **Accessible.** Correct roles + `aria-*` (e.g. `role="progressbar"`, `aria-valuenow/min/max`), keyboard where due.
- **Tested before shown.** Each component gets a shadow-root render test in `TestsShared/Tests` (12/12 harness is
  live; `dotnet run --project TestRunner`). Demo wiring comes after green.

## Architecture recap (how a component is built)
- `SpawnDev.SpawnJS.RazorUI/<Ui*>.razor` `@inherits UiComponentBase`, renders
  `class="@CssClass("ui-x ...")" style="@Style(...)" @attributes="PassThroughAttributes"`.
- Styles: append rules to `wwwroot/razor-ui/base.css` using `var(--ui-*)`.
- Tokens: `wwwroot/razor-ui/theme.light.css` + `theme.dark.css` (`:host,:root { --ui-*: ... }`).
- Theme service `RazorUITheme` (DI singleton, `IBackgroundService`) registers base+theme sheets on the renderer;
  `SetTheme(name)` live-swaps. Fingerprinting disabled on the RCL so the generated bundle filename is stable.

---

## Status

| Area | Component | State | Test | Demo |
|------|-----------|-------|------|------|
| Core | SpawnDomRenderer, RazorUITheme, base/theme CSS | ✅ done | 12/12 harness | ✅ |
| Buttons | `UiButton` (accent/secondary) | ✅ exists | via demo | ✅ |
| Layout | `UiCard`, `UiStack` (row/col + gap) | ✅ exists | - | ✅ |
| Inputs | `UiInput` (`@bind-Value`) | ✅ exists | - | ✅ |
| Overlays | `UiDialog` (backdrop + z-index) | ✅ exists | - | ✅ |
| Feedback | `UiProgressBar` (linear: determinate/indeterminate/striped/label) | ✅ done | ✅ 2 tests | ✅ (awaiting TJ visual confirm) |
| Feedback | `UiProgressCircle` (donut/circular SVG: value/size/stroke/indeterminate) | ✅ done | ✅ 2 tests (incl. real dashoffset geometry) | ✅ confirmed |
| Media/Inputs | `UiSlider` (themed range: `@bind-Value`, Min/Max/Step, filled track) | ✅ done | ✅ 2 tests (render + two-way drag) | ✅ (drives the Progress demo) |
| Media/Icons | `UiIcon` (inline-SVG set: play/pause/stop/skip/volume/fullscreen) | ✅ done | ✅ shared with UiIconButton | ✅ media preview |
| Media/Icons | `UiIconButton` (circular ghost button, Icon/ChildContent, OnClick, aria-label) | ✅ done | ✅ 3 tests (icon shape, label, click) | ✅ media preview |

Legend: ✅ done · 🔜 in progress/next · ⬜ backlog

---

## Roadmap (prioritized)

### Milestone 1 - Progress (IN PROGRESS)
- `UiProgressBar` - linear. Params: `Value`, `Min=0`, `Max=100`, `Indeterminate`, `Striped`, `ShowValue`,
  `Label?`. Determinate = fill width %; indeterminate = animated sweep; accessible.
- `UiProgressCircle` - circular/donut via inline SVG ring (two `<circle>`: track + fill via
  `stroke-dasharray`/`stroke-dashoffset`). Params: `Value`, `Min`, `Max`, `Size`, `StrokeWidth`, `Indeterminate`
  (spin), `ShowValue`. Center label.
- Tests: DOM structure + `aria-valuenow` + fill width / dashoffset + indeterminate class. Demo showroom row.

### Milestone 2 - Media player primitives (feeds custom video/audio players)
- `UiSlider` / `UiRange` (seek + volume; `@bind-Value`, drag, keyboard, buffered ranges overlay).
- `UiIconButton` + an icon story (inline SVG icon set: play/pause/stop/mute/volume/fullscreen/skip).
- `UiTime` label (mm:ss / hh:mm:ss). Then a `UiMediaControls` bar composing the above + `UiProgressBar` buffer.

### Milestone 3 - Feedback & small UI (broad app coverage, Radzen-informed)
- `UiBadge`, `UiChip`/`UiChipList`, `UiTooltip`, `UiSkeleton` (loading placeholder), `UiAlert`,
  `UiNotification`/toast host, `UiSpinner`.

### Milestone 4 - Navigation & layout
- `UiTabs`, `UiBreadcrumb`, `UiMenu`/`UiContextMenu`, `UiAccordion`, `UiSplitter`, `UiAppBar`/`UiSidebar`.

### Milestone 5 - Data (the big one)
- `UiTree` (lazy children) - file browser backbone.
- `UiVirtualList` - windowed rendering + async range load callback (`LoadRange(start,count)`), the core Radzen
  `LoadData` pattern, so thousands+ items stream in by window. `@key`ed rows (our renderer honors `@key` 1:1).
- `UiDataGrid` on top of `UiVirtualList`: columns, sort, filter, paging OR virtualization, async data source.
- `UiPager`.

### Milestone 6 - File browser (composes Data + Nav)
- `UiFileBrowser` - Tree + virtualized list/grid + breadcrumb + context menu + icons. Reference TJ's existing
  ones in `Projects\SpawnDev` and related. OPFS/file-source-agnostic data provider.

### Backlog / later
- Charts (Line/Bar/Column/Area/Pie/**Donut**/Scatter/Gauge) - Radzen has a huge set; pick what apps need.
- `UiDropZone` (drag-drop), `UiColorPicker`, `UiDatePicker`, `UiDropDown`/`UiSelect`, `UiSwitch`, `UiCheckbox`,
  `UiRadioGroup`, `UiNumeric`, `UiRating`, `UiUpload`, `UiScheduler`, `UiTimeline`, `UiQRCode`.

## Conventions for adding a component (checklist)
1. `Ui<Name>.razor` `@inherits UiComponentBase`; variants via computed base class string into `CssClass(...)`.
2. Styles → `base.css` (`var(--ui-*, fallback)`); new tokens → both theme files.
3. Accessibility: role + `aria-*`.
4. Test in `TestsShared/Tests/Ui<Name>Tests.cs`; register the class in `TestSuiteRunner.TestTypes`; run
   `dotnet run --project TestRunner` to green.
5. Add to the demo (`RazorRendererDemo/App.razor`) once it looks right, for TJ to confirm.
6. Update the Status table + Roadmap here. Commit (bugfix/feature increment = push-worthy, Rule 10).
