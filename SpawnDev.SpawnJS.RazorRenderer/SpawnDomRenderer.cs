using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using IComponent = Microsoft.AspNetCore.Components.IComponent;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// An <em>interactive</em> Blazor renderer that runs entirely in .NET WASM (no Blazor JS runtime, no
/// circuit) and applies each <see cref="RenderBatch"/> straight to the real DOM through SpawnJS.
/// <para>
/// This is a faithful C# port of Blazor's <c>BrowserRenderer</c>/<c>LogicalElements</c> (the code that
/// normally lives in <c>Web.JS</c> and consumes the render batch on the JS side). Here the batch never
/// leaves .NET: the same edit stream is walked in C# and every mutation is a typed SpawnJS DOM call.
/// Real <c>@onclick</c>, <c>@bind</c>, <see cref="StateHasChanged"/>, component lifecycle and event
/// callbacks all work - the component model is genuine Blazor, only the renderer is ours.
/// </para>
/// <para>Registered as a DI singleton (see <c>AddRazorRenderer</c>); one renderer hosts many root components.</para>
/// </summary>
public sealed class SpawnDomRenderer : Renderer
{
    const string SvgNamespace = "http://www.w3.org/2000/svg";

    readonly SpawnJSRuntime _js;
    readonly Document _document;
    readonly Dispatcher _dispatcher = Dispatcher.CreateDefault();

    /// <summary>Maps a Blazor component id to the logical element it renders into.</summary>
    readonly Dictionary<int, LogicalElement> _componentLocations = new();

    /// <summary>Root logical elements whose (host) contents must be cleared on their first render.</summary>
    readonly HashSet<LogicalElement> _clearOnFirstRender = new();

    /// <summary>
    /// Maps a captured <c>@ref</c> id (the immutable <see cref="ElementReference.Id"/> the framework
    /// generates once per element insert) to the logical element that owns it. This is how a component
    /// resolves an <see cref="ElementReference"/> back to its live SpawnJS node <em>without</em> a document
    /// query - so it works identically inside a shadow root, where <c>getElementById</c>/<c>querySelector</c>
    /// cannot reach.
    /// </summary>
    readonly Dictionary<string, LogicalElement> _refCaptures = new();

    /// <summary>Constructs the renderer. Called by DI; components resolve services from <paramref name="serviceProvider"/>.</summary>
    public SpawnDomRenderer(IServiceProvider serviceProvider, SpawnJSRuntime js)
        : this(serviceProvider, js, NullLoggerFactory.Instance) { }

    /// <summary>Constructs the renderer with an explicit logger factory.</summary>
    public SpawnDomRenderer(IServiceProvider serviceProvider, SpawnJSRuntime js, ILoggerFactory loggerFactory)
        : base(serviceProvider, loggerFactory)
    {
        _js = js;
        _document = _js.Get<Document>("document")!;
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
    /// Renders <paramref name="componentType"/> as a root component into <paramref name="host"/>.
    /// </summary>
    public Task<int> RenderComponentAsync(Type componentType, Node host, Dictionary<string, object?>? parameters = null)
    {
        var parameterView = parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters);
        return Dispatcher.InvokeAsync(async () =>
        {
            var rootLogical = new LogicalElement { Node = host };
            var component = InstantiateComponent(componentType);
            var componentId = AssignRootComponentId(component);
            _componentLocations[componentId] = rootLogical;
            _clearOnFirstRender.Add(rootLogical);
            await RenderRootComponentAsync(componentId, parameterView);
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
        return Task.CompletedTask;
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

        // Parse the raw markup into detached nodes. appendChild adopts them into this document.
        using var parser = new DOMParser();
        using var parsed = parser.ParseFromString(markupFrame.MarkupContent, "text/html");
        using var body = parsed.Body!;

        var logicalSiblingIndex = 0;
        while (true)
        {
            var first = body.FirstChild;
            if (first is null) break;
            InsertLogicalChild(new LogicalElement { Node = first }, container, logicalSiblingIndex++);
        }
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
