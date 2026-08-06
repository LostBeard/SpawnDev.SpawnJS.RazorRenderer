using SpawnDev.SpawnJS.RazorUI;

namespace RazorRendererTests
{
    /// <summary><see cref="UiTime"/> formats seconds as mm:ss, escalating to hh:mm:ss past an hour.</summary>
    public class UiTimeTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public UiTimeTests(IServiceProvider services) : base(services) { }

        async Task<string?> RenderTextAsync(Dictionary<string, object?> parameters)
        {
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<UiTime>(host, parameters);
            var r = NewRenderer(mappings);
            await r.Ready;
            return TextOf(host, ".ui-time");
        }

        /// <summary>Under an hour renders mm:ss with zero-padding.</summary>
        [RendererTest]
        public async Task FormatsMinutesSecondsTest()
        {
            HostCapabilities.RequireBrowser();
            Assert.Equal("00:00", await RenderTextAsync(new() { ["Seconds"] = 0.0 }), "zero");
            Assert.Equal("02:05", await RenderTextAsync(new() { ["Seconds"] = 125.0 }), "125s");
            Assert.Equal("00:09", await RenderTextAsync(new() { ["Seconds"] = 9.7 }), "9.7s floors to 9");
        }

        /// <summary>At/over an hour renders hh:mm:ss.</summary>
        [RendererTest]
        public async Task FormatsHoursWhenOverAnHourTest()
        {
            HostCapabilities.RequireBrowser();
            Assert.Equal("1:01:01", await RenderTextAsync(new() { ["Seconds"] = 3661.0 }), "3661s");
            Assert.Equal("0:00:30", await RenderTextAsync(new() { ["Seconds"] = 30.0, ["ShowHours"] = true }), "forced hours");
        }
    }
}
