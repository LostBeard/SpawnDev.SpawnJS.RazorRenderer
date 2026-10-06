using RazorRendererTests.Components;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>
    /// <c>@on{event}:preventDefault</c> / <c>:stopPropagation</c>, and Blazor's typed event args for every event family
    /// (pointer, wheel, touch, drag, clipboard, progress, error). Each case asserts the SIDE EFFECT: the checkbox that
    /// stays unchecked, the parent handler that did not run, the values a typed handler received.
    /// </summary>
    public class EventFlagAndArgsTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public EventFlagAndArgsTests(IServiceProvider services) : base(services) { }

        async Task<Element> MountAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] TComponent>() where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<TComponent>(host);
            var r = NewRenderer(mappings);
            await r.Ready;
            return host;
        }

        void Click(Element host, string selector)
        {
            using var el = host.QuerySelector<HTMLElement>(selector);
            Assert.NotNull(el, $"{selector} not found");
            el!.Click();
        }

        bool IsChecked(Element host, string selector)
        {
            using var el = host.QuerySelector<HTMLInputElement>(selector);
            return el!.Checked;
        }

        /// <summary>A plain JS object with the given members (an event's init dictionary).</summary>
        SpawnJSObject Init(params (string Key, object? Value)[] members)
        {
            var o = new SpawnJSObject(JS.New("Object"));
            foreach (var (k, v) in members) o.JSRef!.Set(k, v);
            return o;
        }

        void Dispatch(Element host, string selector, string eventClass, string type, SpawnJSObject init)
        {
            using var el = host.QuerySelector<Element>(selector);
            Assert.NotNull(el, $"{selector} not found");
            using var ev = new Event(JS.New(eventClass, type, init));
            el!.DispatchEvent(ev);
            init.Dispose();
        }

        /// <summary>
        /// <c>@onclick:preventDefault</c> on a checkbox: the handler runs, the box stays unchecked (the default action -
        /// toggling it - was prevented), and the flag is not written into the DOM as an attribute.
        /// </summary>
        [RendererTest]
        public async Task PreventDefaultBlocksTheDefaultActionTest()
        {
            var host = await MountAsync<EventFlagsBox>();
            Click(host, "[data-prevent]");
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-prevent-clicks]") == "1"), "the handler did not run");
            Assert.False(IsChecked(host, "[data-prevent]"), "preventDefault did not stop the checkbox toggling");
            using var el = host.QuerySelector<Element>("[data-prevent]");
            Assert.False(el!.HasAttribute("__internal_preventDefault_onclick"), "the flag was written into the DOM as an attribute");
        }

        /// <summary>
        /// <c>@onclick:preventDefault="cond"</c>: while true the click is prevented; the handler turns it off, and the next
        /// click toggles the box - the flag's REMOVAL reaches the live listener.
        /// </summary>
        [RendererTest]
        public async Task PreventDefaultCanBeTurnedOffTest()
        {
            var host = await MountAsync<EventFlagsBox>();
            Click(host, "[data-prevent-toggle]");
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-prevent-on]") == "off"), "the handler did not run");
            Assert.False(IsChecked(host, "[data-prevent-toggle]"), "the first click (flag on) toggled the box");
            Click(host, "[data-prevent-toggle]");
            Assert.True(await WaitForAsync(() => IsChecked(host, "[data-prevent-toggle]")), "after the flag was turned off the click still did not toggle the box");
        }

        /// <summary>
        /// <c>@onclick:stopPropagation</c>: the element's handler runs, its parent's does not. A button without it bubbles
        /// (the control), and the flag alone - with no handler on the element - still stops it.
        /// </summary>
        [RendererTest]
        public async Task StopPropagationKeepsTheEventFromTheParentTest()
        {
            var host = await MountAsync<EventFlagsBox>();
            Click(host, "[data-bubble]");
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-child-clicks]") == "1" && TextOf(host, "[data-parent-clicks]") == "1"),
                $"control: a click without the flag must reach both (child {TextOf(host, "[data-child-clicks]")}, parent {TextOf(host, "[data-parent-clicks]")})");
            Click(host, "[data-stop]");
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-child-clicks]") == "2"), "the stopping element's own handler did not run");
            Click(host, "[data-stop-only]");
            await Task.Delay(150);   // anything that was going to reach the parent has by now
            Assert.Equal("1", TextOf(host, "[data-parent-clicks]"), "the parent's handler ran for a click that stopped propagation");
        }

        [RendererTest]
        public async Task PointerEventArgsTest()
        {
            var host = await MountAsync<EventArgsBox>();
            Dispatch(host, "[data-pointer]", "PointerEvent", "pointerdown", Init(("bubbles", true), ("pointerId", 7), ("width", 23.5),
                ("height", 7.25), ("pointerType", "pen"), ("isPrimary", true), ("clientX", 11.5), ("pressure", 0.5)));
            const string want = "pointer pointerdown id=7 w=23.5 h=7.25 type=pen primary=True x=11.5 pressure=0.5";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }

        [RendererTest]
        public async Task WheelEventArgsTest()
        {
            var host = await MountAsync<EventArgsBox>();
            Dispatch(host, "[data-wheel]", "WheelEvent", "wheel", Init(("bubbles", true), ("deltaY", 120.5), ("deltaMode", 1), ("clientX", 4)));
            const string want = "wheel wheel dy=120.5 mode=1 x=4";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }

        [RendererTest]
        public async Task TouchEventArgsTest()
        {
            var host = await MountAsync<EventArgsBox>();
            if (!JS.Has("TouchEvent") || !JS.Has("Touch")) throw new SkipTestException("this browser has no TouchEvent / Touch constructors");
            using var target = host.QuerySelector<Element>("[data-touch]");
            using var touch = new SpawnJSObject(JS.New("Touch", Init(("identifier", 3), ("target", target), ("clientX", 12.5))));
            using var list = new SpawnJSObject(JS.New("Array"));
            list.JSRef!.CallVoid("push", touch);
            Dispatch(host, "[data-touch]", "TouchEvent", "touchstart", Init(("bubbles", true), ("touches", list), ("changedTouches", list), ("ctrlKey", true)));
            const string want = "touch touchstart n=1 id=3 x=12.5 changed=1 ctrl=True";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }

        [RendererTest]
        public async Task DragEventArgsTest()
        {
            var host = await MountAsync<EventArgsBox>();
            using var dt = new DataTransfer();
            dt.SetData("text/plain", "hello");
            // outside a real drag a DataTransfer is not writable: effectAllowed stays what the browser says ("none")
            var effect = dt.EffectAllowed;
            Dispatch(host, "[data-drag]", "DragEvent", "dragstart", Init(("bubbles", true), ("dataTransfer", dt)));
            var want = $"drag dragstart types=text/plain effect={effect} items=1 item0=string:text/plain";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }

        [RendererTest]
        public async Task ClipboardEventArgsTest()
        {
            var host = await MountAsync<EventArgsBox>();
            Dispatch(host, "[data-copy]", "ClipboardEvent", "copy", Init(("bubbles", true)));
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == "clipboard copy"), $"saw '{TextOf(host, "[data-last]")}'");
        }

        [RendererTest]
        public async Task ProgressEventArgsTest()
        {
            var host = await MountAsync<EventArgsBox>();
            Dispatch(host, "[data-progress]", "ProgressEvent", "progress", Init(("lengthComputable", true), ("loaded", 5), ("total", 10)));
            const string want = "progress progress 5/10 computable=True";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }

        [RendererTest]
        public async Task ErrorEventArgsTest()
        {
            var host = await MountAsync<EventArgsBox>();
            Dispatch(host, "[data-error]", "ErrorEvent", "error", Init(("message", "boom"), ("lineno", 3), ("colno", 9), ("filename", "a.js")));
            const string want = "error error boom line=3 col=9 file=a.js";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }

        /// <summary>
        /// A broken image fires a plain Event named 'error' (not an ErrorEvent): the handler still gets ErrorEventArgs,
        /// with the ErrorEvent-only members absent.
        /// </summary>
        [RendererTest]
        public async Task PlainErrorEventFromAnImageTest()
        {
            var host = await MountAsync<EventArgsBox>();
            using (var img = host.QuerySelector<HTMLImageElement>("[data-img-error]"))
                img!.Src = "data:image/png;base64,AA==";
            const string want = "error error  line=0 col=0 file=";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }

        /// <summary>A script-made DragEvent may carry no dataTransfer: the handler gets an empty one, not a crash.</summary>
        [RendererTest]
        public async Task DragEventWithoutDataTransferTest()
        {
            var host = await MountAsync<EventArgsBox>();
            Dispatch(host, "[data-drag]", "DragEvent", "dragstart", Init(("bubbles", true)));
            Assert.True(await WaitForAsync(() => (TextOf(host, "[data-last]") ?? "").StartsWith("drag dragstart types= ")),
                $"saw '{TextOf(host, "[data-last]")}'");
            Assert.True((TextOf(host, "[data-last]") ?? "").Contains("items=0"), $"saw '{TextOf(host, "[data-last]")}'");
        }

        /// <summary>MouseEventArgs also carries Detail and MovementX/Y (Blazor's do).</summary>
        [RendererTest]
        public async Task MouseEventArgsDetailAndMovementTest()
        {
            var host = await MountAsync<EventArgsBox>();
            Dispatch(host, "[data-mouse]", "MouseEvent", "mousedown", Init(("bubbles", true), ("detail", 2), ("movementX", 5), ("buttons", 1)));
            const string want = "mouse mousedown detail=2 mx=5 buttons=1";
            Assert.True(await WaitForAsync(() => TextOf(host, "[data-last]") == want), $"saw '{TextOf(host, "[data-last]")}', expected '{want}'");
        }
    }
}
