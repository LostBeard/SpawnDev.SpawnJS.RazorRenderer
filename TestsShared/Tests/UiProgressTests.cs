using System.Globalization;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorUI;

namespace RazorRendererTests
{
    /// <summary>
    /// RazorUI progress components: the linear <see cref="UiProgressBar"/> and the circular/donut
    /// <see cref="UiProgressCircle"/>, in determinate and indeterminate modes.
    /// </summary>
    public class UiProgressTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public UiProgressTests(IServiceProvider services) : base(services) { }

        async Task<Element> RenderAsync<T>(Dictionary<string, object?> parameters) where T : Microsoft.AspNetCore.Components.IComponent
        {
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<T>(host, parameters);
            var r = NewRenderer(mappings);
            await r.Ready;
            var el = host.QuerySelector("[role=progressbar]");
            Assert.NotNull(el, $"{typeof(T).Name} did not render a role=progressbar root");
            return el!;
        }

        /// <summary>A determinate bar sets aria-valuenow, drives the fill width, and shows a % label.</summary>
        [RendererTest]
        public async Task ProgressBarDeterminateTest()
        {
            HostCapabilities.RequireBrowser();
            using var root = await RenderAsync<UiProgressBar>(new() { ["Value"] = 42.0, ["ShowValue"] = true });
            Assert.Equal("42", root.GetAttribute("aria-valuenow"), "aria-valuenow");

            using var fill = root.QuerySelector(".ui-progress-fill");
            Assert.NotNull(fill, "fill element missing");
            var style = fill!.GetAttribute("style") ?? "";
            Assert.True(style.Replace(" ", "").Contains("width:42%"), $"fill width not 42% (style '{style}')");

            Assert.Equal("42%", TextOf(root, ".ui-progress-label"), "value label");
        }

        /// <summary>An indeterminate bar carries the indeterminate class and omits aria-valuenow.</summary>
        [RendererTest]
        public async Task ProgressBarIndeterminateTest()
        {
            HostCapabilities.RequireBrowser();
            using var root = await RenderAsync<UiProgressBar>(new() { ["Indeterminate"] = true });
            var cls = root.GetAttribute("class") ?? "";
            Assert.True(cls.Contains("ui-progress-indeterminate"), $"missing indeterminate class (class '{cls}')");
            Assert.Null(root.GetAttribute("aria-valuenow"), "indeterminate bar must not set aria-valuenow");
        }

        /// <summary>
        /// A determinate donut renders an SVG ring whose fill arc is value-driven: stroke-dasharray = the
        /// circumference and stroke-dashoffset = circumference*(1-fraction). Verifies the real geometry.
        /// </summary>
        [RendererTest]
        public async Task ProgressCircleDeterminateGeometryTest()
        {
            HostCapabilities.RequireBrowser();
            const double stroke = 8, value = 50;
            using var root = await RenderAsync<UiProgressCircle>(new() { ["Value"] = value, ["StrokeWidth"] = stroke });
            Assert.Equal("50", root.GetAttribute("aria-valuenow"), "aria-valuenow");

            using var fill = root.QuerySelector(".ui-progress-circle-fill");
            Assert.NotNull(fill, "SVG fill circle missing - did the renderer emit the <circle> in the SVG namespace?");

            var radius = 50 - stroke / 2;
            var circ = 2 * Math.PI * radius;
            var dashArray = ParseNum(fill!.GetAttribute("stroke-dasharray"));
            var dashOffset = ParseNum(fill!.GetAttribute("stroke-dashoffset"));
            Assert.True(Math.Abs(dashArray - circ) < 1.0, $"stroke-dasharray {dashArray} != circumference {circ:F2}");
            Assert.True(Math.Abs(dashOffset - circ * 0.5) < 1.0, $"stroke-dashoffset {dashOffset} != half-circumference {circ * 0.5:F2} for 50%");
        }

        /// <summary>An indeterminate donut carries the indeterminate class and omits aria-valuenow.</summary>
        [RendererTest]
        public async Task ProgressCircleIndeterminateTest()
        {
            HostCapabilities.RequireBrowser();
            using var root = await RenderAsync<UiProgressCircle>(new() { ["Indeterminate"] = true });
            var cls = root.GetAttribute("class") ?? "";
            Assert.True(cls.Contains("ui-progress-circle-indeterminate"), $"missing indeterminate class (class '{cls}')");
            Assert.Null(root.GetAttribute("aria-valuenow"), "indeterminate donut must not set aria-valuenow");
        }

        static double ParseNum(string? s) => double.Parse(s ?? "0", CultureInfo.InvariantCulture);
    }
}
