using Microsoft.AspNetCore.Components;
using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>
    /// <c>@ref</c> resolution through the renderer (not <c>document</c>), so it works in the light DOM and,
    /// critically, inside a shadow root where <c>getElementById</c>/<c>querySelector</c> from the document
    /// cannot reach.
    /// </summary>
    public class RefTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public RefTests(IServiceProvider services) : base(services) { }

        /// <summary>An <c>@ref</c> on a light-DOM element resolves to its live wrapper.</summary>
        [RendererTest]
        public async Task RefResolvesInLightDomTest() => await RunRefTest(useShadow: false);

        /// <summary>An <c>@ref</c> inside a shadow root resolves - the whole point of renderer-based resolution.</summary>
        [RendererTest]
        public async Task RefResolvesInShadowRootTest() => await RunRefTest(useShadow: true);

        async Task RunRefTest(bool useShadow)
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            ElementReference captured = default;
            var parameters = new Dictionary<string, object?>
            {
                ["OnRef"] = (Action<ElementReference>)(er => captured = er),
            };
            var mappings = NewMappings();
            if (useShadow)
                mappings.Add<RefBox>(host, new AttachShadowRootOptions { Mode = "open" }, parameters);
            else
                mappings.Add<RefBox>(host, parameters);
            var r = NewRenderer(mappings);
            await r.Ready;

            var fired = await WaitForAsync(() => !string.IsNullOrEmpty(captured.Id));
            Assert.True(fired, "@ref capture callback never fired");

            using var el = r.GetElement<HTMLElement>(captured);
            Assert.NotNull(el, "@ref did not resolve to a live element via the renderer");
            Assert.Equal("ref target", el!.TextContent, "resolved @ref points at the wrong element");
        }
    }
}
