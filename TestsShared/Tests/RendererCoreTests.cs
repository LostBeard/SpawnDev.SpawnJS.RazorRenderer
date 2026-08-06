using Microsoft.AspNetCore.Components;
using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>Core rendering: a component mounts into a light-DOM host, and into an encapsulated shadow root.</summary>
    public class RendererCoreTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public RendererCoreTests(IServiceProvider services) : base(services) { }

        /// <summary>A component rendered into a plain element appears in that element's light DOM.</summary>
        [RendererTest]
        public async Task MountRendersComponentInLightDomTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<Counter>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            using var counter = host.QuerySelector("[data-counter]");
            Assert.NotNull(counter, "Counter component did not render into the light-DOM host");
            Assert.Equal("0", TextOf(host, "[data-count]"), "initial counter text");
        }

        /// <summary>
        /// A component rendered into a host's shadow root is encapsulated there - present in
        /// <c>host.shadowRoot</c>, absent from the host's light DOM.
        /// </summary>
        [RendererTest]
        public async Task ShadowRootEncapsulatesContentTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<Counter>(host, new AttachShadowRootOptions { Mode = "open" });
            var r = NewRenderer(mappings);
            await r.Ready;

            using (var light = host.QuerySelector("[data-counter]"))
                Assert.Null(light, "content leaked into the light DOM - it should be encapsulated in the shadow root");

            using var shadow = host.ShadowRoot;
            Assert.NotNull(shadow, "host has no shadowRoot");
            using (var inShadow = shadow!.QuerySelector("[data-counter]"))
                Assert.NotNull(inShadow, "Counter did not render inside the shadow root");
            Assert.Equal("0", TextOf(shadow, "[data-count]"), "initial counter text in shadow");
        }
    }
}
