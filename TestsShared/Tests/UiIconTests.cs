using Microsoft.AspNetCore.Components;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorUI;

namespace RazorRendererTests
{
    /// <summary><see cref="UiIcon"/> renders the requested SVG shape; <see cref="UiIconButton"/> labels and fires clicks.</summary>
    public class UiIconTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public UiIconTests(IServiceProvider services) : base(services) { }

        /// <summary>A named icon renders an SVG with the matching child geometry (filled and outline shapes).</summary>
        [RendererTest]
        public async Task IconRendersRequestedShapeTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<UiIcon>(host, new Dictionary<string, object?> { ["Name"] = "play" });
            var r = NewRenderer(mappings);
            await r.Ready;

            using (var svg = host.QuerySelector(".ui-icon"))
                Assert.NotNull(svg, "UiIcon did not render an svg.ui-icon");
            using (var poly = host.QuerySelector(".ui-icon polygon"))
                Assert.NotNull(poly, "play icon should render a <polygon> (SVG child namespace working)");
        }

        /// <summary>An icon button carries its aria-label and renders the named icon inside.</summary>
        [RendererTest]
        public async Task IconButtonRendersLabelAndIconTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<UiIconButton>(host, new Dictionary<string, object?> { ["Icon"] = "pause", ["AriaLabel"] = "Pause" });
            var r = NewRenderer(mappings);
            await r.Ready;

            using var btn = host.QuerySelector(".ui-icon-button");
            Assert.NotNull(btn, "icon button not found");
            Assert.Equal("Pause", btn!.GetAttribute("aria-label"), "aria-label");
            using var rect = host.QuerySelector(".ui-icon-button .ui-icon rect");
            Assert.NotNull(rect, "pause icon should render <rect>s inside the button");
        }

        /// <summary>Clicking an icon button invokes its OnClick callback.</summary>
        [RendererTest]
        public async Task IconButtonClickFiresOnClickTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var clicked = false;
            var mappings = NewMappings();
            mappings.Add<UiIconButton>(host, new Dictionary<string, object?>
            {
                ["Icon"] = "play",
                ["AriaLabel"] = "Play",
                ["OnClick"] = EventCallback.Factory.Create(this, () => clicked = true),
            });
            var r = NewRenderer(mappings);
            await r.Ready;

            using (var btn = host.QuerySelector<HTMLElement>(".ui-icon-button"))
            {
                Assert.NotNull(btn, "icon button not found");
                btn!.Click();
            }

            var ok = await WaitForAsync(() => clicked);
            Assert.True(ok, "icon button click did not fire OnClick");
        }
    }
}
