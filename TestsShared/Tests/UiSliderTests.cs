using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorUI;

namespace RazorRendererTests
{
    /// <summary>The themed range <see cref="UiSlider"/> - initial render and two-way drag propagation.</summary>
    public class UiSliderTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public UiSliderTests(IServiceProvider services) : base(services) { }

        /// <summary>Renders a range input at the given value with a fill variable set to its fraction.</summary>
        [RendererTest]
        public async Task SliderRendersFilledTrackTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<UiSlider>(host, new Dictionary<string, object?> { ["Value"] = 25.0, ["Max"] = 100.0 });
            var r = NewRenderer(mappings);
            await r.Ready;

            using var input = host.QuerySelector<HTMLInputElement>(".ui-slider");
            Assert.NotNull(input, "slider input not found");
            Assert.Equal("range", input!.GetAttribute("type"), "slider must be a range input");
            Assert.Equal("25", input.Value, "initial slider value");
            var style = input.GetAttribute("style") ?? "";
            Assert.True(style.Replace(" ", "").Contains("--ui-slider-fill:25%"), $"fill not 25% (style '{style}')");
        }

        /// <summary>
        /// Dragging (set value + dispatch 'input') flows through OnInput -> Value -> re-render, so the fill
        /// variable follows the new value. Proves the two-way loop end to end.
        /// </summary>
        [RendererTest]
        public async Task SliderReflectsDraggedValueTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<UiSlider>(host, new Dictionary<string, object?> { ["Value"] = 30.0 });
            var r = NewRenderer(mappings);
            await r.Ready;

            using (var input = host.QuerySelector<HTMLInputElement>(".ui-slider"))
            {
                Assert.NotNull(input, "slider input not found");
                Assert.Equal("30", input!.Value, "initial slider value");
                input.Value = "70";
                using var ev = new Event("input");
                input.DispatchEvent(ev);
            }

            var ok = await WaitForAsync(() =>
            {
                using var input = host.QuerySelector(".ui-slider");
                var style = input?.GetAttribute("style") ?? "";
                return style.Replace(" ", "").Contains("--ui-slider-fill:70%");
            });
            Assert.True(ok, "dragging the slider did not update the fill to 70%");
        }
    }
}
