using Microsoft.Extensions.DependencyInjection;
using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorRenderer;

namespace RazorRendererTests
{
    /// <summary>
    /// <see cref="UiVirtualList{TItem}"/> windowing + async range loading over 1000 items (item value ==
    /// index). Rendered via the singleton renderer so the list's injected renderer matches the one resolving
    /// its <c>@ref</c> scroll container.
    /// </summary>
    public class UiVirtualListTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public UiVirtualListTests(IServiceProvider services) : base(services) { }

        async Task<Element> RenderHarnessAsync()
        {
            var host = NewHost();
            var renderer = Services.GetRequiredService<SpawnDomRenderer>();
            await renderer.RenderComponentAsync<VirtualListHarness>(host);
            return host;
        }

        /// <summary>Only a small window of rows is in the DOM, not all 1000 - and the top row loaded.</summary>
        [RendererTest]
        public async Task VirtualizesInsteadOfRenderingAllRowsTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await RenderHarnessAsync();

            var loaded = await WaitForAsync(() =>
            {
                using var r0 = host.QuerySelector("[data-index='0']");
                return r0 != null;
            });
            Assert.True(loaded, "first window never loaded (item 0 missing)");

            using var rows = host.QuerySelectorAll(".vrow");
            Assert.True(rows.Length is > 0 and < 60, $"expected a small window, got {rows.Length} rows of 1000");
        }

        /// <summary>Scrolling loads and renders the new window and virtualizes the old one out.</summary>
        [RendererTest]
        public async Task ScrollingLoadsAndRendersNewWindowTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await RenderHarnessAsync();
            await WaitForAsync(() =>
            {
                using var r0 = host.QuerySelector("[data-index='0']");
                return r0 != null;
            });

            // scroll to row 250 (250 * 20px = 5000)
            using (var container = host.QuerySelector<HTMLElement>(".ui-virtual-list"))
            {
                Assert.NotNull(container, "scroll container not found");
                container!.ScrollTop = 5000;
                using var ev = new Event("scroll");
                container.DispatchEvent(ev);
            }

            var appeared = await WaitForAsync(() =>
            {
                using var r = host.QuerySelector("[data-index='250']");
                return r != null;
            });
            Assert.True(appeared, "scrolling did not load/render item 250");

            using var top = host.QuerySelector("[data-index='0']");
            Assert.Null(top, "item 0 should be virtualized out after scrolling to 250");
        }
    }
}
