using RazorRendererTests.Components;

namespace RazorRendererTests
{
    /// <summary>
    /// Documents (and guards) how a parent re-render propagates to children - which is standard Blazor
    /// diff behavior, faithfully reproduced by SpawnDomRenderer:
    ///  - a child whose PARAMETERS are unchanged (e.g. parameterless) is NOT re-rendered when its parent
    ///    re-renders (the diff skips it - this is the optimization ShouldRender complements);
    ///  - a child whose parameter CHANGED IS re-rendered.
    /// Practical consequence: a component that shows external/service state must receive it as a changed
    /// parameter or subscribe to a change event (as Gemineachy's Taskbar/AppFrame do). This was verified
    /// empirically - the base RenderTreeDiffBuilder (not SpawnDomRenderer) skips the unchanged child.
    /// </summary>
    public class ParentChildReRenderTests : RendererTestBase
    {
        public ParentChildReRenderTests(IServiceProvider services) : base(services) { }

        [RendererTest]
        public async Task ParentReRenderReRendersOnlyChildrenWithChangedParamsTest()
        {
            HostCapabilities.RequireBrowser();
            ReRenderState.Value = 0;
            ReRenderParent.Instance = null;

            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<ReRenderParent>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            Assert.Equal("0", TextOf(host, "[data-rr-parent]"), "initial parent text");
            Assert.Equal("0", TextOf(host, "[data-rr-child]"), "initial parameterless child text");
            Assert.Equal("0", TextOf(host, "[data-rr-childparam]"), "initial param child text");

            var parent = ReRenderParent.Instance;
            Assert.NotNull(parent, "parent instance was not captured");

            // Parent-only re-render on the dispatcher (mirrors AppDesktop reacting to AppManager.OnChanged).
            await r.Dispatcher.InvokeAsync(() => parent!.Bump());
            await WaitForAsync(() => TextOf(host, "[data-rr-parent]") == "1");

            Assert.Equal("1", TextOf(host, "[data-rr-parent]"), "parent DOM should reflect the re-render");
            // Standard Blazor: the parameterless child is skipped (its parameters didn't change).
            Assert.Equal("0", TextOf(host, "[data-rr-child]"),
                "parameterless child should NOT re-render on a parent-only re-render (Blazor diff optimization)");
            // ...but the child whose parameter changed IS re-rendered.
            Assert.Equal("1", TextOf(host, "[data-rr-childparam]"),
                "child with a changed parameter SHOULD re-render");
        }
    }
}
