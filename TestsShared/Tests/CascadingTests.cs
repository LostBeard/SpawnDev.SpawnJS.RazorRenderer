using RazorRendererTests.Components;

namespace RazorRendererTests
{
    /// <summary>
    /// Verifies the renderer flows <c>CascadingValue</c> into a child's <c>[CascadingParameter]</c> - the
    /// mechanism child-component registration (e.g. UiDataGrid columns) depends on.
    /// </summary>
    public class CascadingTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public CascadingTests(IServiceProvider services) : base(services) { }

        /// <summary>A cascaded value reaches a nested child's [CascadingParameter].</summary>
        [RendererTest]
        public async Task CascadingValueReachesChildTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<CascadeParent>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            using var el = host.QuerySelector("[data-cascade='cascaded-42']");
            Assert.NotNull(el, "CascadingValue did not reach the child's [CascadingParameter]");
        }
    }
}
