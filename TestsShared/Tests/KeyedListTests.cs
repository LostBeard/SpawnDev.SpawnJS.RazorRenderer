using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>
    /// <c>@key</c> gives each item a stable 1:1 element lifetime. Removing a MIDDLE item removes exactly that
    /// item's physical node and leaves every survivor's node in place - not Blazor's keyless positional shift
    /// that would mutate survivors and drop the tail node.
    /// </summary>
    public class KeyedListTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public KeyedListTests(IServiceProvider services) : base(services) { }

        /// <summary>
        /// Stamp a non-render expando on each item = its text, remove the middle item, then verify only the
        /// middle left and every survivor still carries its original stamp (same physical node, no shift).
        /// </summary>
        [RendererTest]
        public async Task RemoveMiddleKeepsSurvivorIdentityTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<KeyedList>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            using (var before = host.QuerySelectorAll("[data-item]"))
            {
                Assert.Equal(5, before.Length, "expected 5 items before removal");
                for (var i = 0; i < before.Length; i++)
                {
                    using var item = before.Item(i)!;
                    // data-birth is set by the test, never by the render - the diff can't touch it
                    item.SetAttribute("data-birth", item.TextContent ?? "");
                }
            }

            using (var button = host.QuerySelector<HTMLElement>("[data-remove-middle]"))
            {
                Assert.NotNull(button, "remove-middle button not found");
                button!.Click();
            }

            var reduced = await WaitForAsync(() =>
            {
                using var items = host.QuerySelectorAll("[data-item]");
                return items.Length == 4;
            });
            Assert.True(reduced, "list did not drop to 4 items after removing the middle");

            using var after = host.QuerySelectorAll("[data-item]");
            for (var i = 0; i < after.Length; i++)
            {
                using var item = after.Item(i)!;
                var text = item.TextContent ?? "";
                Assert.False(text == "C", "removed item 'C' is still present");
                Assert.Equal(text, item.GetAttribute("data-birth"),
                    $"survivor '{text}' is not on its original physical node - a positional shift occurred");
            }
        }
    }
}
