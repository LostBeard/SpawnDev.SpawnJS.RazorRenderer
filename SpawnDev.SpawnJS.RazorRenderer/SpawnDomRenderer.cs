using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpawnDev.SpawnJS.JSObjects;
using System.ComponentModel;
using IComponent = Microsoft.AspNetCore.Components.IComponent;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// An <em>interactive</em> Blazor renderer that runs entirely in .NET WASM (no Blazor JS runtime, no
/// circuit) and applies each <see cref="RenderBatch"/> straight to the real DOM through SpawnJS.
/// <para>
/// This is a faithful C# port of Blazor's <c>BrowserRenderer</c>/<c>LogicalElements</c> (the code that
/// normally lives in <c>Web.JS</c> and consumes the render batch on the JS side). Here the batch never
/// leaves .NET: the same edit stream is walked in C# and every mutation is a typed SpawnJS DOM call.
/// Real <c>@onclick</c>, <c>@bind</c>, <c>StateHasChanged</c>, component lifecycle and event
/// callbacks all work - the component model is genuine Blazor, only the renderer is ours.
/// </para>
/// <para>
/// Registered as a DI singleton by <c>AddRazorRenderer()</c>; one renderer hosts many root components. As an
/// <see cref="IAsyncBackgroundService"/>, when it starts in a <see cref="GlobalScope.Window"/> scope it renders
/// every registered <see cref="SpawnJSRootComponentMapping"/> (from <c>SpawnJSAppBuilder.RootComponents</c>) into
/// its target - an existing element, a selector, a new element, or a shadow root - so a component tree mounts at
/// startup much like Blazor's <c>WebAssemblyHostBuilder.RootComponents</c>.
/// </para>
/// <para>
/// It also owns the app's <see cref="SharedStyleSheet"/>s: sheets registered here (or via
/// <c>RootComponents.AddSharedStyleSheet</c>) are attached as <c>&lt;link&gt;</c> elements into the document (for
/// light-DOM roots) and into each shadow root (which do not inherit document styles), and are repointed or removed
/// live as the sheet changes.
/// </para>
/// </summary>
public sealed class SpawnDomRenderer : Renderer, IBackgroundService
{
    /// <summary>
    /// Resolves once SpawnDomRenderer.InitAsync has finished loading<br/>
    /// WARNING: Do NOT await this inside IAsyncBackgroundService.Ready Task in another service as <br/>
    /// SpawnDomRenderer does not init until AFTER all IAsyncBackgroundService.Ready Tasks have completed.<br/>
    /// </summary>
    public Task Ready
    {
        get
        {
            // if _backgroundServiceManager is null and !_initStarted start InitAsync
            if (_backgroundServiceManager is null && !_initStarted) _ = InitAsync();
            return _init.Task;
        }
    }

    TaskCompletionSource _init = new TaskCompletionSource();

    /// <summary>
    /// Raised after each render batch has been applied to the DOM. <c>firstRender</c> is true only for
    /// the first batch. Mirrors <see cref="Microsoft.AspNetCore.Components.ComponentBase"/>'s
    /// OnAfterRender, but at the renderer level so a background <b>service</b> (which is not a component)
    /// can react to rendering - e.g. run one-time work once the UI exists. Handlers run on the renderer
    /// Dispatcher, so they may safely touch component state. Exceptions are routed to the renderer's
    /// error handler and never break the render.
    /// </summary>
    public event Action<bool>? OnAfterRender;
    /// <summary>
    /// Async counterpart of <see cref="OnAfterRender"/>. Raised after each render batch is applied.
    /// Handlers are invoked on the Dispatcher but are NOT awaited by the render pipeline (fire-and-forget
    /// with fault observation), so a long-running handler will not block rendering. <c>firstRender</c> is
    /// true only for the first batch.
    /// </summary>
    public event Func<bool, Task>? OnAfterRenderAsync;
    bool _hasRendered;

    const string SvgNamespace = "http://www.w3.org/2000/svg";

    readonly SpawnJSRuntime _js;
    readonly Document _document;
    readonly Dispatcher _dispatcher = Dispatcher.CreateDefault();

    /// <summary>Maps a Blazor component id to the logical element it renders into.</summary>
    readonly Dictionary<int, LogicalElement> _componentLocations = new();

    /// <summary>Root logical elements whose (host) contents must be cleared on their first render.</summary>
    readonly HashSet<LogicalElement> _clearOnFirstRender = new();

    Uri _appBaseUri;

    /// <summary>
    /// Maps a captured <c>@ref</c> id (the immutable <see cref="ElementReference.Id"/> the framework
    /// generates once per element insert) to the logical element that owns it. This is how a component
    /// resolves an <see cref="ElementReference"/> back to its live SpawnJS node <em>without</em> a document
    /// query - so it works identically inside a shadow root, where <c>getElementById</c>/<c>querySelector</c>
    /// cannot reach.
    /// </summary>
    readonly Dictionary<string, LogicalElement> _refCaptures = new();

    // ── Trusted Types ──
    // DOMParser.parseFromString is a Trusted Types injection sink. On a host page whose CSP enforces
    // `require-trusted-types-for 'script'` (e.g. YouTube, Gmail) it throws
    // "This document requires 'TrustedHTML'" for a plain string, which used to abort a render batch
    // mid-mutation (swallowed by HandleException) and leave the shadow DOM half-updated. On such pages we
    // route markup through a policy; on every other page (no Trusted Types) we pass the raw string as before.
    TrustedTypePolicy? _markupPolicy;
    bool _markupPolicyResolved;
    Callback? _markupCreateHtml;

    SpawnJSRootComponentMappingCollection _rootComponentMappings;
    /// <summary>
    /// Root component mappings
    /// </summary>
    public IEnumerable<SpawnJSRootComponentMapping> RootComponentMappings => _rootComponentMappings;

    IBackgroundServiceManager? _backgroundServiceManager;

    /// <summary>Constructs the renderer. Called by DI; components resolve services from <paramref name="serviceProvider"/>.</summary>
    public SpawnDomRenderer(IServiceProvider serviceProvider, SpawnJSRuntime js, SpawnJSRootComponentMappingCollection rootComponentMappings)
        : this(serviceProvider, js, rootComponentMappings, null!, NullLoggerFactory.Instance) { }

    /// <summary>Constructs the renderer. Called by DI; components resolve services from <paramref name="serviceProvider"/>.</summary>
    public SpawnDomRenderer(IServiceProvider serviceProvider, SpawnJSRuntime js, SpawnJSRootComponentMappingCollection rootComponentMappings, IBackgroundServiceManager backgroundServiceManager)
        : this(serviceProvider, js, rootComponentMappings, backgroundServiceManager, NullLoggerFactory.Instance) { }

    /// <summary>Constructs the renderer with an explicit logger factory.</summary>
    public SpawnDomRenderer(IServiceProvider serviceProvider, SpawnJSRuntime js, SpawnJSRootComponentMappingCollection rootComponentMappings, IBackgroundServiceManager backgroundServiceManager, ILoggerFactory loggerFactory)
        : base(serviceProvider, loggerFactory)
    {
        _js = js;
        _document = _js.Get<Document>("document")!;
        _rootComponentMappings = rootComponentMappings;
        _appBaseUri = new Uri(_js.AppBaseUri);
        if (_js.GlobalScope == GlobalScope.Window)
        {
            foreach (var styleSheetUrl in _rootComponentMappings.StyleSheets)
            {
                TryAddSharedStyleSheet(styleSheetUrl, out _);
            }
        }
        _backgroundServiceManager = backgroundServiceManager;
        // SpawnDomRenderer uses post IAsyncBackgroundService startup which means all IBackgroundService and IAsyncBackgroundService services have started completely.
        // This way startup mirrors how it works with SpawnDev.BlazorJS where no razor pages are rendered until AFTER all IAsyncBackgroundService services have started completely.
        // if _backgroundServiceManager is null, the first get of Ready will start InitAsync
        _backgroundServiceManager?.OnStarted += (bgServiceManager, globalScope) => InitAsync();
    }
    bool _initStarted = false;
    /// <summary>
    /// The service BackgroundServiceManager will autostart this service be cause it implements IAsyncBackgroundService<br/>
    /// Used to render registered Window components on start up when in a Window global scope
    /// </summary>
    /// <returns></returns>
    private async Task InitAsync()
    {
        if (_initStarted) return;
        _initStarted = true;
        try
        {
            // Question: Do we need to wait for DOMContentLoaded event / readyState != "loading"?
            // Render any _rootComponentMappings IF JS.GlobalScope == Window
            if (_js.GlobalScope == GlobalScope.Window)
            {
                if (_document != null && _rootComponentMappings.Any())
                {
                    UpdateDocumentCSS(true);
                    foreach (var component in _rootComponentMappings)
                    {
                        await RenderMapping(component);
                    }
                }
            }
            _init.TrySetResult();
        }
        catch (Exception ex)
        {
            _init.TrySetException(ex);
        }
    }
    /// <summary>
    /// This method adds are remove the 
    /// </summary>
    void UpdateDocumentCSS(bool skipRemoveCheck = false)
    {
        var anyNonShadow = _rootComponentMappings.Any(o => o.ShadowRootOptions == null);
        if (anyNonShadow)
        {
            // becuase there are non-shadow root mappings we add the style sheets to the document
            // add the style sheets to the shadow root (they don't inherit from document)
            foreach (var styleSheetUrl in SharedStyleSheets)
            {
                using var linkElement = GetStyleSheet(_document, styleSheetUrl.Href);
                if (linkElement != null) continue;
                using var linkElementNew = AttachStyleSheet(_document, styleSheetUrl.Href);
            }
        }
        else if (!skipRemoveCheck)
        {
            // none. remove the css
            foreach (var styleSheetUrl in SharedStyleSheets)
            {
                using var linkElement = GetStyleSheet(_document, styleSheetUrl.Href);
                linkElement?.Remove();
            }
        }
    }
    /// <summary>
    /// Shared style sheets
    /// </summary>
    public IEnumerable<SharedStyleSheet> SharedStyleSheets => _SharedStyleSheets;
    List<SharedStyleSheet> _SharedStyleSheets = new List<SharedStyleSheet>();
    /// <summary>
    /// Add a SharedStyleSheet
    /// </summary>
    /// <param name="styleSheetUrl"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public SharedStyleSheet AddSharedStyleSheet(string styleSheetUrl)
    {
        var url = new Uri(_appBaseUri, styleSheetUrl).ToString();
        if (_SharedStyleSheets.Any(o => o.Href == url)) throw new Exception("Already exists");
        var sharedStyleSheet = new SharedStyleSheet(url);
        sharedStyleSheet.OnStyleSheetsChanged += CssStyleSheet_OnStyleSheetsChanged;
        sharedStyleSheet.OnStyleSheetRemoved += CssStyleSheet_OnStyleSheetsChanged;
        _SharedStyleSheets.Add(sharedStyleSheet);
        return sharedStyleSheet;
    }
    /// <summary>
    /// Tries adding a new SharedStyleSheet usign the specified styleSheetUrl and returne ture if a new one was added<br/>
    /// sharedStyleSheet will be set to an the existing SharedStyleSheet if one exists or the new one if one was created
    /// </summary>
    /// <param name="styleSheetUrl"></param>
    /// <param name="sharedStyleSheet"></param>
    /// <returns></returns>
    public bool TryAddSharedStyleSheet(string styleSheetUrl, out SharedStyleSheet sharedStyleSheet)
    {
        var url = new Uri(_appBaseUri, styleSheetUrl).ToString();
        sharedStyleSheet = _SharedStyleSheets.FirstOrDefault(o => o.Href == url)!;
        if (sharedStyleSheet != null) return false;
        sharedStyleSheet = new SharedStyleSheet(url);
        sharedStyleSheet.OnStyleSheetsChanged += CssStyleSheet_OnStyleSheetsChanged;
        sharedStyleSheet.OnStyleSheetRemoved += CssStyleSheet_OnStyleSheetsChanged;
        _SharedStyleSheets.Add(sharedStyleSheet);
        return true;
    }
    /// <summary>
    /// Returns the SharedStyleSheet associeted wit hthe specified styleSheetUrl or null
    /// </summary>
    /// <param name="styleSheetUrl"></param>
    /// <returns></returns>
    public SharedStyleSheet? GetSharedStyleSheet(string styleSheetUrl)
    {
        var url = new Uri(_appBaseUri, styleSheetUrl).ToString();
        return _SharedStyleSheets.FirstOrDefault(o => o.Href == url);
    }
    /// <summary>
    /// Remove a SharedStyleSheet
    /// </summary>
    /// <param name="sharedStyleSheetUrl"></param>
    /// <returns></returns>
    public bool RemoveSharedStyleSheet(string sharedStyleSheetUrl)
    {
        var sharedStyleSheet = GetSharedStyleSheet(sharedStyleSheetUrl);
        if (sharedStyleSheet == null) return false;
        return RemoveSharedStyleSheet(sharedStyleSheet);
    }
    /// <summary>
    /// Remove a SharedStyleSheet
    /// </summary>
    /// <param name="sharedStyleSheet"></param>
    /// <returns></returns>
    public bool RemoveSharedStyleSheet(SharedStyleSheet sharedStyleSheet)
    {
        if (sharedStyleSheet == null || !_SharedStyleSheets.Contains(sharedStyleSheet)) return false;
        sharedStyleSheet.OnStyleSheetsChanged -= CssStyleSheet_OnStyleSheetsChanged;
        sharedStyleSheet.OnStyleSheetRemoved -= CssStyleSheet_OnStyleSheetsChanged;
        _SharedStyleSheets.Remove(sharedStyleSheet);
        sharedStyleSheet.Remove();  // this will make sure Removed is true on the SharedStyleSheet
        // remove from document
        var nonShadowRoot = _rootComponentMappings.Any(o => o.ShadowRoot == null);
        if (nonShadowRoot)
        {
            using var linkElement = GetStyleSheet(_document, sharedStyleSheet.Href);
            linkElement?.Remove();
        }
        // remove from shadow dom root nodes
        foreach (var component in _rootComponentMappings)
        {
            if (component.ShadowRoot != null)
            {
                using var linkElement = GetStyleSheet(component.ShadowRoot, sharedStyleSheet.Href);
                linkElement?.Remove();
            }
        }
        return true;
    }
    void CssStyleSheet_OnStyleSheetsChanged(SharedStyleSheet sharedStyleSheet)
    {
        RemoveSharedStyleSheet(sharedStyleSheet);
    }
    private void CssStyleSheet_OnStyleSheetsChanged(SharedStyleSheet sharedStyleSheet, string oldHref)
    {
        // remove from document
        var nonShadowRoot = _rootComponentMappings.Any(o => o.ShadowRoot == null);
        if (nonShadowRoot)
        {
            using var linkElement = GetStyleSheet(_document, oldHref);
            linkElement?.SetAttribute("href", sharedStyleSheet.Href);
        }
        // remove from shadow dom root nodes
        foreach (var component in _rootComponentMappings)
        {
            if (component.ShadowRoot != null)
            {
                using var linkElement = GetStyleSheet(component.ShadowRoot, oldHref);
                linkElement?.SetAttribute("href", sharedStyleSheet.Href);
            }
        }
    }

    async Task RenderMapping(SpawnJSRootComponentMapping component)
    {
        if (_js.GlobalScope != GlobalScope.Window) return;
        // render
        if (component.Host == null)
        {
            if (!string.IsNullOrEmpty(component.Selector))
            {
                component.Host = _document.QuerySelector(component.Selector);
                if (component.Host == null)
                {
                    // should we throw, log it, ignore it (host not found)?
                    return;
                }
            }
            else
            {
                // when neither a Host or a Selector ar specified it means we simply create a new host
                component.Host = _document.CreateElement("div");
                using var body = _document.Body;
                body!.Append(component.Host);
            }
        }
        // Apply the component.HostStyle (if one was set)
        if (!string.IsNullOrEmpty(component.HostStyle))
        {
            component.Host.SetAttribute("style", component.HostStyle);
        }
        // if component.ShadowRootOptions is set, we are rendering to the host shadow root instead of the host itself
        if (component.ShadowRootOptions != null)
        {
            component.ShadowRoot = component.Host.AttachShadow(component.ShadowRootOptions);
            // add the style sheets to the shadow root (they don't inherit from document)
            foreach (var styleSheetUrl in SharedStyleSheets)
            {
                using var linkElement = AttachStyleSheet(component.ShadowRoot, styleSheetUrl.Href);
            }
            // fire the host config callback now that the host is known (style sheets already applied)
            if (component.ConfigureHostCallback != null)
            {
                await component.ConfigureHostCallback(component);
            }
            // render the component
            component.ComponentId = await RenderComponentAsync(component.ComponentType, component.ShadowRoot, component.Parameters);
        }
        else
        {
            // fire the host config callback now that the host is known (style sheets already applied)
            if (component.ConfigureHostCallback != null)
            {
                await component.ConfigureHostCallback(component);
            }
            // render the component
            component.ComponentId = await RenderComponentAsync(component.ComponentType, component.Host, component.Parameters);
        }
    }

    /// <inheritdoc/>
    public override Dispatcher Dispatcher => _dispatcher;

    /// <inheritdoc/>
    protected override void HandleException(Exception exception)
        => _js.LogError($"[SpawnDomRenderer] Unhandled render exception: {exception}");

    // ───────────────────────────────────────────────────────── mounting ──

    /// <summary>
    /// Renders <typeparamref name="TComponent"/> as a root component into <paramref name="host"/> (an
    /// element or shadow root). The host's existing children are cleared. Returns the component id.
    /// </summary>
    public Task<int> RenderComponentAsync<TComponent>(Node host, Dictionary<string, object?>? parameters = null)
        where TComponent : IComponent
        => RenderComponentAsync(typeof(TComponent), host, parameters);

    /// <summary>
    /// Renders <typeparamref name="TComponent"/> as a root component into <paramref name="host"/> with the
    /// given root parameters.
    /// </summary>
    public Task<int> RenderComponentAsync<TComponent>(Node host, ParameterView parameters)
        where TComponent : IComponent
        => RenderComponentAsync(typeof(TComponent), host, parameters);

    /// <summary>
    /// Renders <paramref name="componentType"/> as a root component into <paramref name="host"/>.
    /// </summary>
    public Task<int> RenderComponentAsync(Type componentType, Node host, Dictionary<string, object?>? parameters = null)
        => RenderComponentAsync(componentType, host, parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters));

    /// <summary>
    /// Renders <paramref name="componentType"/> as a root component into <paramref name="host"/> with the
    /// given root parameters.
    /// </summary>
    public Task<int> RenderComponentAsync(Type componentType, Node host, ParameterView parameters)
    {
        return Dispatcher.InvokeAsync(async () =>
        {
            var rootLogical = new LogicalElement { Node = host };
            var component = InstantiateComponent(componentType);
            var componentId = AssignRootComponentId(component);
            _componentLocations[componentId] = rootLogical;
            _clearOnFirstRender.Add(rootLogical);
            await RenderRootComponentAsync(componentId, parameters);
            return componentId;
        });
    }

    // ─────────────────────────────────────────────── element references ──

    /// <summary>
    /// Resolves an <see cref="ElementReference"/> captured with <c>@ref</c> to the live SpawnJS node the
    /// renderer created for it, reinterpreted as <typeparamref name="T"/> (e.g. <c>HTMLVideoElement</c>).
    /// Returns <see langword="null"/> if the reference is default or its element has left the tree.
    /// <para>
    /// Unlike a <c>document.getElementById</c>/<c>querySelector</c> lookup, this reaches the element through
    /// the renderer's own logical tree, so it works identically whether the component is mounted in the light
    /// DOM or inside a shadow root. The returned wrapper is a fresh JS reference the caller owns and disposes.
    /// </para>
    /// </summary>
    public T? GetElement<T>(ElementReference reference) where T : SpawnJSObject
        => string.IsNullOrEmpty(reference.Id) ? null
         : _refCaptures.TryGetValue(reference.Id, out var le) ? le.Node.JSRefAs<T>()
         : null;

    /// <summary>
    /// Resolves an <see cref="ElementReference"/> to the underlying <see cref="Node"/> the renderer holds for
    /// it (shadow-root safe, no document query). Returns <see langword="null"/> if unresolved. Prefer
    /// <see cref="GetElement{T}"/> when a typed wrapper is wanted.
    /// </summary>
    public Node? GetElementNode(ElementReference reference)
        => string.IsNullOrEmpty(reference.Id) ? null
         : _refCaptures.TryGetValue(reference.Id, out var le) ? le.Node
         : null;

    // ───────────────────────────────────────────────────── stylesheets ──

    // adoptedStyleSheets is a read-only property whose value is a mutable ObservableArray - you PUSH a sheet
    // into it, you do not reassign it (that is why the SpawnJS wrappers expose it get-only). ShadowRoot and
    // Document each carry it (the DocumentOrShadowRoot mixin), so these are typed overloads rather than one
    // Node method - no reaching around the wrappers.

    /// <summary>
    /// Creates a constructable stylesheet from <paramref name="css"/> and adopts it into a shadow root, so
    /// its rules apply INSIDE that root - the only way to style a component mounted in a shadow root, since a
    /// host-page <c>&lt;link&gt;</c>/<c>&lt;style&gt;</c> cannot cross the boundary.
    /// <para>The returned sheet is shareable: adopt the SAME sheet into many roots and a later
    /// <see cref="CSSStyleSheet.ReplaceSync"/> updates every one at once (live theme switching). Caller owns it.</para>
    /// </summary>
    public CSSStyleSheet AdoptStyleSheet(ShadowRoot root, string css)
    {
        var sheet = MakeStyleSheet(css);
        AdoptStyleSheet(root, sheet);
        return sheet;
    }

    /// <summary>Adopts an existing (possibly shared) <paramref name="sheet"/> into a shadow root.</summary>
    public void AdoptStyleSheet(ShadowRoot root, CSSStyleSheet sheet)
    {
        using var adopted = root.AdoptedStyleSheets;
        adopted.Push(sheet);
    }

    /// <summary>
    /// Creates a constructable stylesheet from <paramref name="css"/> and adopts it into a document (the
    /// light-DOM equivalent - applies to everything the document renders). Returns the shareable sheet.
    /// </summary>
    public CSSStyleSheet AdoptStyleSheet(Document root, string css)
    {
        var sheet = MakeStyleSheet(css);
        AdoptStyleSheet(root, sheet);
        return sheet;
    }

    /// <summary>Adopts an existing (possibly shared) <paramref name="sheet"/> into a document.</summary>
    public void AdoptStyleSheet(Document root, CSSStyleSheet sheet)
    {
        using var adopted = root.AdoptedStyleSheets;
        adopted.Push(sheet);
    }

    static CSSStyleSheet MakeStyleSheet(string css)
    {
        var sheet = new CSSStyleSheet();
        sheet.ReplaceSync(css);
        return sheet;
    }

    // Link-based alternative to AdoptStyleSheet: loads a stylesheet BY URL through code (no page edit needed).
    // A <link> appended INTO a shadow root loads and scopes to that root; appended to the document head it
    // covers the light DOM. This is how an app self-loads all its CSS - its own, a theme file, or the
    // build's auto-generated {App}.styles.css scoped bundle - with only the app script on the page.

    /// <summary>
    /// Returns the link element with the specified href using querySelector if it exists
    /// </summary>
    public Element? GetStyleSheet(Document document, string href)
    {
        if (document == null) return null;
        var linkElement = document.QuerySelector($"link[rel=\"stylesheet\"][href=\"{href}\"]");
        return linkElement;
    }

    /// <summary>
    /// Returns the link element with the specified href using querySelector if it exists
    /// </summary>
    public Element? GetStyleSheet(ShadowRoot root, string href)
    {
        if (root == null) return null;
        var linkElement = root.QuerySelector($"link[rel=\"stylesheet\"][href=\"{href}\"]");
        return linkElement;
    }

    /// <summary>
    /// Creates a <c>&lt;link rel="stylesheet"&gt;</c> for <paramref name="href"/> and appends it INTO
    /// <paramref name="root"/>, so the sheet loads and applies inside that shadow root. Returns the link
    /// element - change its <c>href</c> later (e.g. to swap a theme) or remove it to unload the sheet.
    /// </summary>
    public Element AttachStyleSheet(ShadowRoot root, string href)
    {
        var link = CreateStyleLink(href);
        root.AppendChild(link);
        return link;
    }

    /// <summary>
    /// Creates a <c>&lt;link rel="stylesheet"&gt;</c> for <paramref name="href"/> and appends it to the
    /// document head (the light-DOM equivalent). Returns the link element.
    /// </summary>
    public Element AttachStyleSheet(Document document, string href)
    {
        var link = CreateStyleLink(href);
        using var head = document.Head!;
        head.AppendChild(link);
        return link;
    }

    Element CreateStyleLink(string href)
    {
        var link = _document.CreateElement("link");
        link.SetAttribute("rel", "stylesheet");
        link.SetAttribute("href", href);
        return link;
    }

    // ─────────────────────────────────────────────────── batch handling ──

    /// <inheritdoc/>
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        var frames = renderBatch.ReferenceFrames.Array;

        var updated = renderBatch.UpdatedComponents;
        for (var i = 0; i < updated.Count; i++)
        {
            var diff = updated.Array[i];
            UpdateComponent(diff.ComponentId, diff.Edits, frames);
        }

        var disposedComponents = renderBatch.DisposedComponentIDs;
        for (var i = 0; i < disposedComponents.Count; i++)
        {
            _componentLocations.Remove(disposedComponents.Array[i]);
        }

        // DisposedEventHandlerIDs need no action: our listeners are keyed by (element, eventName) and are
        // disposed when the element leaves the tree, so a stale handler id can never be dispatched.

        // The DOM now reflects this batch - notify after-render subscribers (services can hook here).
        var firstRender = !_hasRendered;
        _hasRendered = true;
        RaiseAfterRender(firstRender);
        return Task.CompletedTask;
    }

    void RaiseAfterRender(bool firstRender)
    {
        // Runs on the Dispatcher (UpdateDisplayAsync is dispatched), so handlers may touch component state.
        var sync = OnAfterRender;
        if (sync != null)
        {
            try { sync(firstRender); }
            catch (Exception ex) { HandleException(ex); }
        }
        var asyncHandler = OnAfterRenderAsync;
        if (asyncHandler != null)
        {
            // Fire-and-forget each async handler so a long-running one (e.g. a network round-trip) does
            // NOT block the render pipeline; faults are observed and routed to the renderer's handler.
            foreach (Func<bool, Task> d in asyncHandler.GetInvocationList())
                _ = InvokeAfterRenderAsync(d, firstRender);
        }
    }

    async Task InvokeAfterRenderAsync(Func<bool, Task> handler, bool firstRender)
    {
        try { await handler(firstRender); }
        catch (Exception ex) { HandleException(ex); }
    }

    void UpdateComponent(int componentId, ArrayBuilderSegment<RenderTreeEdit> edits, RenderTreeFrame[] frames)
    {
        var element = _componentLocations[componentId];

        if (_clearOnFirstRender.Remove(element))
        {
            EmptyLogicalElement(element);
            if (element.Node is Comment comment) comment.TextContent = "!";
        }

        ApplyEdits(componentId, element, 0, edits, frames);
    }

    // ─────────────────────────────────────────────────────── applyEdits ──

    void ApplyEdits(int componentId, LogicalElement parentAtStart, int childIndex,
        ArrayBuilderSegment<RenderTreeEdit> edits, RenderTreeFrame[] frames)
    {
        var parent = parentAtStart;
        var currentDepth = 0;
        var childIndexAtCurrentDepth = childIndex;
        List<(int fromSiblingIndex, int toSiblingIndex)>? permutationList = null;

        for (var editIndex = 0; editIndex < edits.Count; editIndex++)
        {
            var edit = edits[editIndex];
            switch (edit.Type)
            {
                case RenderTreeEditType.PrependFrame:
                    {
                        var frameIndex = edit.ReferenceFrameIndex;
                        var frame = frames[frameIndex];
                        var siblingIndex = edit.SiblingIndex;
                        InsertFrame(componentId, parent, childIndexAtCurrentDepth + siblingIndex, frames, frame, frameIndex);
                        break;
                    }
                case RenderTreeEditType.RemoveFrame:
                    {
                        RemoveLogicalChild(parent, childIndexAtCurrentDepth + edit.SiblingIndex);
                        break;
                    }
                case RenderTreeEditType.SetAttribute:
                    {
                        var frame = frames[edit.ReferenceFrameIndex];
                        var target = GetLogicalChild(parent, childIndexAtCurrentDepth + edit.SiblingIndex);
                        if (target.Node is Element)
                            ApplyAttribute(componentId, target, frame);
                        else
                            throw new InvalidOperationException("Cannot set attribute on non-element child");
                        break;
                    }
                case RenderTreeEditType.RemoveAttribute:
                    {
                        var target = GetLogicalChild(parent, childIndexAtCurrentDepth + edit.SiblingIndex);
                        if (target.Node is Element)
                            SetOrRemoveAttributeOrProperty(target, edit.RemovedAttributeName!, null);
                        else
                            throw new InvalidOperationException("Cannot remove attribute from non-element child");
                        break;
                    }
                case RenderTreeEditType.UpdateText:
                    {
                        var frame = frames[edit.ReferenceFrameIndex];
                        var textLogical = GetLogicalChild(parent, childIndexAtCurrentDepth + edit.SiblingIndex);
                        if (textLogical.Node is Text)
                            textLogical.Node.TextContent = frame.TextContent;
                        else
                            throw new InvalidOperationException("Cannot set text content on non-text child");
                        break;
                    }
                case RenderTreeEditType.UpdateMarkup:
                    {
                        var frame = frames[edit.ReferenceFrameIndex];
                        var siblingIndex = childIndexAtCurrentDepth + edit.SiblingIndex;
                        RemoveLogicalChild(parent, siblingIndex);
                        InsertMarkup(parent, siblingIndex, frame);
                        break;
                    }
                case RenderTreeEditType.StepIn:
                    {
                        parent = GetLogicalChild(parent, childIndexAtCurrentDepth + edit.SiblingIndex);
                        currentDepth++;
                        childIndexAtCurrentDepth = 0;
                        break;
                    }
                case RenderTreeEditType.StepOut:
                    {
                        parent = parent.Parent!;
                        currentDepth--;
                        childIndexAtCurrentDepth = currentDepth == 0 ? childIndex : 0;
                        break;
                    }
                case RenderTreeEditType.PermutationListEntry:
                    {
                        permutationList ??= new();
                        permutationList.Add((childIndexAtCurrentDepth + edit.SiblingIndex,
                                             childIndexAtCurrentDepth + edit.MoveToSiblingIndex));
                        break;
                    }
                case RenderTreeEditType.PermutationListEnd:
                    {
                        PermuteLogicalChildren(parent, permutationList!);
                        permutationList = null;
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Unknown edit type: {edit.Type}");
            }
        }
    }

    // ───────────────────────────────────────────────────── frame insert ──

    int InsertFrame(int componentId, LogicalElement parent, int childIndex, RenderTreeFrame[] frames,
        RenderTreeFrame frame, int frameIndex)
    {
        switch (frame.FrameType)
        {
            case RenderTreeFrameType.Element:
                InsertElement(componentId, parent, childIndex, frames, frame, frameIndex);
                return 1;
            case RenderTreeFrameType.Text:
                InsertText(parent, childIndex, frame);
                return 1;
            case RenderTreeFrameType.Attribute:
                throw new InvalidOperationException("Attribute frames should only be leading children of element frames.");
            case RenderTreeFrameType.Component:
                InsertComponent(parent, childIndex, frame);
                return 1;
            case RenderTreeFrameType.Region:
                return InsertFrameRange(componentId, parent, childIndex, frames, frameIndex + 1, frameIndex + SubtreeLength(frame));
            case RenderTreeFrameType.ElementReferenceCapture:
                if (parent.Node is Element)
                {
                    // The framework has already generated this id and set the component's ElementReference
                    // field (RenderTreeDiffBuilder.InitializeNewElementReferenceCaptureFrame, before this
                    // batch reached us). Record the id -> node mapping so the component can resolve it.
                    parent.ElementReferenceCaptureId = frame.ElementReferenceCaptureId;
                    _refCaptures[frame.ElementReferenceCaptureId] = parent;
                    return 0;
                }
                throw new InvalidOperationException("Reference capture frames can only be children of element frames.");
            case RenderTreeFrameType.Markup:
                InsertMarkup(parent, childIndex, frame);
                return 1;
            case RenderTreeFrameType.NamedEvent:
                return 0;
            default:
                throw new InvalidOperationException($"Unknown frame type: {frame.FrameType}");
        }
    }

    int InsertFrameRange(int componentId, LogicalElement parent, int childIndex, RenderTreeFrame[] frames,
        int startIndex, int endIndexExcl)
    {
        var origChildIndex = childIndex;
        for (var index = startIndex; index < endIndexExcl; index++)
        {
            var frame = frames[index];
            var inserted = InsertFrame(componentId, parent, childIndex, frames, frame, index);
            childIndex += inserted;
            index += CountDescendantFrames(frame);
        }
        return childIndex - origChildIndex;
    }

    void InsertElement(int componentId, LogicalElement parent, int childIndex, RenderTreeFrame[] frames,
        RenderTreeFrame frame, int frameIndex)
    {
        var tagName = frame.ElementName;
        Element dom = tagName == "svg" || IsSvgElement(parent)
            ? _document.CreateElementNS(SvgNamespace, tagName)
            : _document.CreateElement(tagName);
        var newElement = new LogicalElement { Node = dom };

        var inserted = false;
        var descendantsEndIndexExcl = frameIndex + SubtreeLength(frame);
        for (var descendantIndex = frameIndex + 1; descendantIndex < descendantsEndIndexExcl; descendantIndex++)
        {
            var descendantFrame = frames[descendantIndex];
            if (descendantFrame.FrameType == RenderTreeFrameType.Attribute)
            {
                ApplyAttribute(componentId, newElement, descendantFrame);
            }
            else
            {
                // Leading attributes done; insert the element then its children in one pass.
                InsertLogicalChild(newElement, parent, childIndex);
                inserted = true;
                InsertFrameRange(componentId, newElement, 0, frames, descendantIndex, descendantsEndIndexExcl);
                break;
            }
        }

        if (!inserted) InsertLogicalChild(newElement, parent, childIndex);
    }

    void InsertText(LogicalElement parent, int childIndex, RenderTreeFrame textFrame)
    {
        var textNode = _document.CreateTextNode(textFrame.TextContent);
        InsertLogicalChild(new LogicalElement { Node = textNode }, parent, childIndex);
    }

    void InsertComponent(LogicalElement parent, int childIndex, RenderTreeFrame frame)
    {
        var container = CreateAndInsertLogicalContainer(parent, childIndex);
        _componentLocations[frame.ComponentId] = container;
    }

    void InsertMarkup(LogicalElement parent, int childIndex, RenderTreeFrame markupFrame)
    {
        var container = CreateAndInsertLogicalContainer(parent, childIndex);

        // Parse the raw markup into detached nodes through a <template>, then adopt them by appendChild.
        //
        // A <template>'s content is parsed in the HTML "template" insertion mode, which keeps head-only
        // elements - <style>, <script>, <link>, <meta>, <title> - as children in source order. Parsing the
        // same string as a full text/html document (the old DOMParser path) instead hoists those into the
        // document <head>; reading only <body> then dropped them, so a component whose static markup was a
        // <style> block rendered as an empty <!--!--> container with no styles. This mirrors Blazor's own
        // BrowserRenderer, which also parses markup via a <template>.
        using var template = _document.CreateElement<HTMLTemplateElement>("template");
        SetMarkup(template, markupFrame.MarkupContent);
        using var content = template.Content;

        var logicalSiblingIndex = 0;
        while (true)
        {
            var first = content.FirstChild;
            if (first is null) break;
            InsertLogicalChild(new LogicalElement { Node = first }, container, logicalSiblingIndex++);
        }
    }

    /// <summary>
    /// Sets a template element's markup, Trusted Types safe. innerHTML is a Trusted Types injection sink: on a
    /// page that enforces Trusted Types the raw string is refused, so the markup goes through a policy that
    /// produces a <see cref="TrustedHTML"/> first; on every other page the raw string is set directly.
    /// </summary>
    void SetMarkup(HTMLTemplateElement template, string markup)
    {
        var policy = GetMarkupPolicy();
        if (policy is null) { template.InnerHTML = markup; return; }
        using var trusted = policy.CreateHTML(markup);
        template.SetInnerHTML(trusted);
    }

    /// <summary>
    /// The Trusted Type policy used to approve markup for a template's <c>innerHTML</c>, or null when the page
    /// does not enforce Trusted Types (the common case - the raw-string assignment works there). Resolved once
    /// and cached: the browser support and CSP do not change over the renderer's life.
    /// </summary>
    TrustedTypePolicy? GetMarkupPolicy()
    {
        if (_markupPolicyResolved) return _markupPolicy;
        _markupPolicyResolved = true;

        using var factory = _js.Get<TrustedTypePolicyFactory?>("trustedTypes");
        if (factory is null) return null; // no Trusted Types (hackaday, a normal window, most workers)

        // Identity createHTML: the output feeds an INERT <template>'s content fragment (no script runs, no
        // live DOM until the nodes are adopted), and the markup is the app's OWN first-party component output,
        // so passing it through unchanged is safe. A Callback (never `new Function`) because a page enforcing
        // Trusted Types usually also blocks unsafe-eval, which would refuse an eval-built function.
        _markupCreateHtml = Callback.Create<string, string>(s => s);
        try
        {
            _markupPolicy = factory.CreatePolicy("spawndev-razorrenderer",
                new TrustedTypePolicyOptions { CreateHTML = _markupCreateHtml });
        }
        catch (Exception ex)
        {
            // A restrictive `trusted-types` CSP allowlist that omits our policy name lands here. Fail loud
            // with an actionable message rather than the silent, half-mutated DOM this used to cause.
            _js.LogError($"[SpawnDomRenderer] Trusted Types is enforced but policy 'spawndev-razorrenderer' could not be created (the page's CSP trusted-types allowlist may block it). Markup will not render on this page. {ex.Message}");
            _markupCreateHtml.Dispose();
            _markupCreateHtml = null;
        }
        return _markupPolicy;
    }

    // ────────────────────────────────────────────────────── attributes ──

    void ApplyAttribute(int componentId, LogicalElement element, RenderTreeFrame attributeFrame)
    {
        var attributeName = attributeFrame.AttributeName;
        var eventHandlerId = attributeFrame.AttributeEventHandlerId;

        if (eventHandlerId != 0)
        {
            SetListener(element, StripOnPrefix(attributeName), eventHandlerId, componentId);
            return;
        }

        SetOrRemoveAttributeOrProperty(element, attributeName, attributeFrame.AttributeValue);
    }

    void SetOrRemoveAttributeOrProperty(LogicalElement element, string name, object? valueOrNullToRemove)
    {
        // Removing an event handler: drop the live DOM listener we own.
        if (valueOrNullToRemove is null && IsOnEventName(name)
            && element.EventListeners is not null && element.EventListeners.ContainsKey(StripOnPrefix(name)))
        {
            RemoveListener(element, StripOnPrefix(name));
            return;
        }

        if (TryApplySpecialProperty(element, name, valueOrNullToRemove)) return;

        var el = (Element)element.Node;
        switch (valueOrNullToRemove)
        {
            case null:
                el.RemoveAttribute(name);
                break;
            case bool b:
                if (b) el.SetAttribute(name, ""); else el.RemoveAttribute(name);
                break;
            default:
                el.SetAttribute(name, valueOrNullToRemove.ToString()!);
                break;
        }
    }

    /// <summary>
    /// Applies attributes that must be set as live DOM <em>properties</em> rather than markup attributes
    /// (so <c>@bind</c> reflects visually and reads back correctly). Mirrors Blazor's DomSpecialPropertyUtil
    /// for the common cases via typed SpawnJS wrappers.
    /// </summary>
    // TODO(rule2): select/textarea/option coverage needs HTMLSelectElement/HTMLTextAreaElement/HTMLOptionElement
    // wrappers added to SpawnJS; HTMLInputElement covers input value/checked today.
    bool TryApplySpecialProperty(LogicalElement element, string name, object? value)
    {
        switch (name)
        {
            case "value":
                {
                    using var input = element.Node.JSRefAs<HTMLInputElement>();
                    input.Value = value as string ?? value?.ToString() ?? "";
                    return true;
                }
            case "checked":
                {
                    using var input = element.Node.JSRefAs<HTMLInputElement>();
                    input.Checked = value is bool b ? b : value is not null;
                    return true;
                }
            default:
                return false;
        }
    }

    // ──────────────────────────────────────────────────────── events ──

    void SetListener(LogicalElement element, string eventName, ulong handlerId, int componentId)
    {
        element.EventListeners ??= new();
        if (element.EventListeners.TryGetValue(eventName, out var existing))
        {
            // Reuse the one live DOM listener; just rebind the handler id.
            existing.HandlerId = handlerId;
            return;
        }

        var registration = new EventListenerRegistration { Callback = null!, EventName = eventName, HandlerId = handlerId };
        var callback = Callback.Create<Event>(ev => OnDomEvent(registration, eventName, ev));
        registration.Callback = callback;
        element.EventListeners[eventName] = registration;
        ((Element)element.Node).AddEventListener(eventName, callback);
    }

    void RemoveListener(LogicalElement element, string eventName)
    {
        if (element.EventListeners is null || !element.EventListeners.Remove(eventName, out var reg)) return;
        ((Element)element.Node).RemoveEventListener(eventName, reg.Callback);
        reg.Callback.Dispose();
    }

    void OnDomEvent(EventListenerRegistration registration, string eventName, Event ev)
    {
        var args = WebEventArgsFactory.Create(eventName, ev);
        _ = DispatchToComponentAsync(registration.HandlerId, args);
    }

    async Task DispatchToComponentAsync(ulong handlerId, EventArgs eventArgs)
    {
        try
        {
            // The DOM event callback runs off the renderer's Dispatcher; marshal onto it before dispatching
            // (which mutates component state and triggers a re-render).
            await Dispatcher.InvokeAsync(() => DispatchEventAsync(handlerId, fieldInfo: null, eventArgs: eventArgs));
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    // ───────────────────────────────────────────── logical tree (port) ──
    // Faithful port of Web.JS LogicalElements.ts, adapted from "the DOM node IS the logical element" to a
    // wrapper class. Physical DOM mutations and the logical child/parent arrays are kept in lockstep.

    LogicalElement CreateAndInsertLogicalContainer(LogicalElement parent, int childIndex)
    {
        var containerComment = _document.CreateComment("!");
        var container = new LogicalElement { Node = containerComment };
        InsertLogicalChild(container, parent, childIndex);
        return container;
    }

    void InsertLogicalChild(LogicalElement child, LogicalElement parent, int childIndex)
    {
        // Detach from any existing logical parent first.
        if (child.Parent is not null)
        {
            child.Parent.Children.Remove(child);
            child.Parent = null;
        }

        var siblings = parent.Children;

        // Prune siblings whose physical node has been removed out-of-band.
        for (var i = siblings.Count - 1; i >= 0; i--)
        {
            if (siblings[i].Node.ParentNode is null)
            {
                siblings.RemoveAt(i);
                if (i < childIndex) childIndex--;
            }
        }

        // The physical range to move: a populated container spans [comment .. last descendant]; anything
        // else (the common freshly-created node) is a single node.
        var firstNode = child.Node;
        var lastNode = child.Node is Comment && child.Children.Count > 0
            ? FindLastDomNodeInRange(child)
            : child.Node;

        if (childIndex < siblings.Count)
        {
            var refNode = siblings[childIndex].Node;
            MoveRangeBefore(firstNode, lastNode, refNode);
            siblings.Insert(childIndex, child);
        }
        else
        {
            AppendRange(firstNode, lastNode, parent);
            siblings.Add(child);
        }

        child.Parent = parent;
    }

    void RemoveLogicalChild(LogicalElement parent, int childIndex)
    {
        var child = parent.Children[childIndex];
        parent.Children.RemoveAt(childIndex);
        DetachSubtree(child);
    }

    /// <summary>
    /// Removes a logical child's physical node(s) and disposes every DOM listener we created in its subtree.
    /// For a comment container the logical children are following siblings and are removed individually;
    /// for an element they are physical descendants that leave with the element (but their listeners are
    /// still ours to dispose).
    /// </summary>
    void DetachSubtree(LogicalElement child)
    {
        DisposeListeners(child);

        if (child.Node is Comment)
        {
            while (child.Children.Count > 0)
            {
                var grandchild = child.Children[0];
                child.Children.RemoveAt(0);
                DetachSubtree(grandchild);
            }
        }
        else
        {
            foreach (var grandchild in child.Children) DisposeListenersDeep(grandchild);
            child.Children.Clear();
        }

        child.Node.ParentNode?.RemoveChild(child.Node);
    }

    void DisposeListeners(LogicalElement element)
    {
        // This element is leaving the tree: forget any captured @ref so a stale id can never resolve to a
        // detached node. Runs before the listener check because a ref-only element has no listeners.
        if (element.ElementReferenceCaptureId is not null)
        {
            _refCaptures.Remove(element.ElementReferenceCaptureId);
            element.ElementReferenceCaptureId = null;
        }

        if (element.EventListeners is null) return;
        foreach (var reg in element.EventListeners.Values) reg.Callback.Dispose();
        element.EventListeners = null;
    }

    void DisposeListenersDeep(LogicalElement element)
    {
        DisposeListeners(element);
        foreach (var child in element.Children) DisposeListenersDeep(child);
    }

    void EmptyLogicalElement(LogicalElement element)
    {
        while (element.Children.Count > 0) RemoveLogicalChild(element, 0);
    }

    static LogicalElement GetLogicalChild(LogicalElement parent, int childIndex) => parent.Children[childIndex];

    static LogicalElement? GetLogicalNextSibling(LogicalElement element)
    {
        var siblings = element.Parent!.Children;
        var index = siblings.IndexOf(element);
        return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
    }

    void MoveRangeBefore(Node first, Node last, Node refNode)
    {
        var parentDom = refNode.ParentNode!;
        var current = first;
        while (current is not null)
        {
            var next = current.NextSibling;
            parentDom.InsertBefore(current, refNode);
            if (current.IsSameNode(last)) break;
            current = next;
        }
    }

    void AppendRange(Node first, Node last, LogicalElement parent)
    {
        var current = first;
        while (current is not null)
        {
            var next = current.NextSibling;
            AppendDomNode(current, parent);
            if (current.IsSameNode(last)) break;
            current = next;
        }
    }

    void AppendDomNode(Node child, LogicalElement parent)
    {
        if (parent.Node is Element || parent.Node is DocumentFragment)
        {
            parent.Node.AppendChild(child);
        }
        else if (parent.Node is Comment)
        {
            var nextSibling = GetLogicalNextSibling(parent);
            if (nextSibling is not null)
            {
                var refNode = nextSibling.Node;
                refNode.ParentNode!.InsertBefore(child, refNode);
            }
            else
            {
                AppendDomNode(child, parent.Parent!);
            }
        }
        else
        {
            throw new InvalidOperationException("Cannot append node: parent is not a valid logical element.");
        }
    }

    Node FindLastDomNodeInRange(LogicalElement element)
    {
        if (element.Node is Element || element.Node is DocumentFragment) return element.Node;

        var nextSibling = GetLogicalNextSibling(element);
        if (nextSibling is not null) return nextSibling.Node.PreviousSibling!;

        var logicalParent = element.Parent!;
        return logicalParent.Node is Element || logicalParent.Node is DocumentFragment
            ? logicalParent.Node.LastChild!
            : FindLastDomNodeInRange(logicalParent);
    }

    void PermuteLogicalChildren(LogicalElement parent, List<(int fromSiblingIndex, int toSiblingIndex)> permutationList)
    {
        var siblings = parent.Children;

        // 1. Snapshot each move's physical range before anything shifts.
        var entries = new PermutationEntry[permutationList.Count];
        for (var i = 0; i < permutationList.Count; i++)
        {
            var (from, to) = permutationList[i];
            var moveStart = siblings[from];
            entries[i] = new PermutationEntry
            {
                FromSiblingIndex = from,
                ToSiblingIndex = to,
                MoveRangeStart = moveStart,
                MoveRangeEnd = FindLastDomNodeInRange(moveStart),
            };
        }

        // 2. Drop a marker comment at each destination.
        foreach (var entry in entries)
        {
            var marker = _document.CreateComment("marker");
            entry.Marker = marker;
            var insertBeforeNode = entry.ToSiblingIndex + 1 < siblings.Count ? siblings[entry.ToSiblingIndex + 1] : null;
            if (insertBeforeNode is not null)
            {
                var refNode = insertBeforeNode.Node;
                refNode.ParentNode!.InsertBefore(marker, refNode);
            }
            else
            {
                AppendDomNode(marker, parent);
            }
        }

        // 3. Move each physical range in front of its marker, then drop the marker.
        foreach (var entry in entries)
        {
            var insertBefore = entry.Marker!;
            var parentDom = insertBefore.ParentNode!;
            var moveEnd = entry.MoveRangeEnd!;
            Node? nextToMove = entry.MoveRangeStart!.Node;
            while (nextToMove is not null)
            {
                var nextNext = nextToMove.NextSibling;
                parentDom.InsertBefore(nextToMove, insertBefore);
                if (nextToMove.IsSameNode(moveEnd)) break;
                nextToMove = nextNext;
            }
            parentDom.RemoveChild(insertBefore);
        }

        // 4. Reorder the logical sibling array to match.
        foreach (var entry in entries)
        {
            siblings[entry.ToSiblingIndex] = entry.MoveRangeStart!;
        }
    }

    bool IsSvgElement(LogicalElement parent)
    {
        var closest = GetClosestDomElement(parent);
        return closest is Element el && el.NamespaceURI == SvgNamespace && el.TagName != "foreignObject";
    }

    static Node GetClosestDomElement(LogicalElement element)
    {
        if (element.Node is Element || element.Node is DocumentFragment) return element.Node;
        if (element.Node is Comment) return element.Node.ParentNode!;
        throw new InvalidOperationException("Not a valid logical element.");
    }

    // ─────────────────────────────────────────────────────────── util ──

    static int SubtreeLength(RenderTreeFrame frame) => frame.FrameType switch
    {
        RenderTreeFrameType.Element => frame.ElementSubtreeLength,
        RenderTreeFrameType.Region => frame.RegionSubtreeLength,
        RenderTreeFrameType.Component => frame.ComponentSubtreeLength,
        _ => 0,
    };

    static int CountDescendantFrames(RenderTreeFrame frame) => frame.FrameType switch
    {
        RenderTreeFrameType.Component => frame.ComponentSubtreeLength - 1,
        RenderTreeFrameType.Element => frame.ElementSubtreeLength - 1,
        RenderTreeFrameType.Region => frame.RegionSubtreeLength - 1,
        _ => 0,
    };

    static bool IsOnEventName(string name) => name.Length > 2 && name[0] == 'o' && name[1] == 'n';

    static string StripOnPrefix(string attributeName)
        => attributeName.StartsWith("on", StringComparison.Ordinal) ? attributeName[2..] : attributeName;

    sealed class PermutationEntry
    {
        public int FromSiblingIndex;
        public int ToSiblingIndex;
        public LogicalElement? MoveRangeStart;
        public Node? MoveRangeEnd;
        public Comment? Marker;
    }
}
