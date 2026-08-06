using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorUI;

namespace RazorRendererTests
{
    /// <summary>
    /// RazorUI's theme service loads base + theme sheets into the shadow root via the renderer, and
    /// <see cref="RazorUITheme.SetTheme"/> live-swaps the theme sheet across the root.
    /// </summary>
    public class RazorUIThemeTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public RazorUIThemeTests(IServiceProvider services) : base(services) { }

        /// <summary>
        /// Construct the theme before Ready (the production construct-before-Ready ordering), mount into a
        /// shadow root, then confirm base + light theme links landed and SetTheme(dark) repoints the theme link.
        /// </summary>
        [RendererTest]
        public async Task ThemeSwitchRepointsThemeLinkInShadowRootTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<Counter>(host, new AttachShadowRootOptions { Mode = "open" });
            var r = NewRenderer(mappings);
            // theme registers base.css + theme.light.css on the renderer BEFORE Ready attaches shared sheets
            var theme = new RazorUITheme(r, JS);
            await r.Ready;

            using var shadow = host.ShadowRoot!;
            using (var baseLink = shadow.QuerySelector("link[href$=\"razor-ui/base.css\"]"))
                Assert.NotNull(baseLink, "RazorUI base.css was not attached into the shadow root");
            using (var lightLink = shadow.QuerySelector("link[href$=\"theme.light.css\"]"))
                Assert.NotNull(lightLink, "RazorUI light theme was not attached into the shadow root");

            theme.SetTheme(RazorUITheme.Dark);

            var swapped = await WaitForAsync(() =>
            {
                using var darkLink = shadow.QuerySelector("link[href$=\"theme.dark.css\"]");
                using var lightLink = shadow.QuerySelector("link[href$=\"theme.light.css\"]");
                return darkLink != null && lightLink == null;
            });
            Assert.True(swapped, "SetTheme(dark) did not repoint the theme <link> in the shadow root");
        }
    }
}
