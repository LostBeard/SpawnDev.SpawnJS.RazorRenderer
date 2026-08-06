using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary><see cref="UiTree{TItem}"/>: renders roots, lazy-loads children on expand, highlights selection.</summary>
    public class UiTreeTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public UiTreeTests(IServiceProvider services) : base(services) { }

        async Task<Element> RenderTreeAsync()
        {
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<TreeHarness>(host);
            var r = NewRenderer(mappings);
            await r.Ready;
            return host;
        }

        /// <summary>Roots render; children are not loaded until their node is expanded.</summary>
        [RendererTest]
        public async Task RendersRootsLazilyTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await RenderTreeAsync();

            using (var a = host.QuerySelector("[data-node='Folder A']")) Assert.NotNull(a, "Folder A missing");
            using (var b = host.QuerySelector("[data-node='Folder B']")) Assert.NotNull(b, "Folder B missing");
            using (var f = host.QuerySelector("[data-node='file1.txt']")) Assert.NotNull(f, "file1.txt missing");
            using (var child = host.QuerySelector("[data-node='Folder A/child1.txt']"))
                Assert.Null(child, "children must not load until the node is expanded");
        }

        /// <summary>Expanding a folder lazily loads and renders its children.</summary>
        [RendererTest]
        public async Task ExpandLoadsChildrenTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await RenderTreeAsync();

            // first node is "Folder A"; click its expand toggle
            using (var toggle = host.QuerySelector<HTMLElement>(".ui-tree-node .ui-tree-toggle"))
            {
                Assert.NotNull(toggle, "expand toggle not found");
                toggle!.Click();
            }

            var loaded = await WaitForAsync(() =>
            {
                using var c = host.QuerySelector("[data-node='Folder A/child1.txt']");
                return c != null;
            });
            Assert.True(loaded, "expanding Folder A did not load its children");
            using var c2 = host.QuerySelector("[data-node='Folder A/child2.txt']");
            Assert.NotNull(c2, "second child missing after expand");
        }

        /// <summary>Clicking a node selects it - its row gets the selected class.</summary>
        [RendererTest]
        public async Task ClickingNodeSelectsItTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await RenderTreeAsync();

            using (var node = host.QuerySelector<HTMLElement>("[data-node='file1.txt']"))
            {
                Assert.NotNull(node, "file1.txt node not found");
                node!.Click();
            }

            var selected = await WaitForAsync(() =>
            {
                using var sel = host.QuerySelector(".ui-tree-row-selected [data-node='file1.txt']");
                return sel != null;
            });
            Assert.True(selected, "clicking file1.txt did not select its row");
        }
    }
}
