using RazorRendererTests.Components;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>
    /// Regression tests for markup containing a <c>&lt;style&gt;</c> element. Blazor emits a component's static
    /// top-level markup (and any <c>MarkupString</c>) as a Markup frame, which the renderer parses into detached
    /// nodes and adopts. The renderer used to parse that string as a full <c>text/html</c> document and read
    /// only its <c>&lt;body&gt;</c>; the HTML parser hoists head-only elements (<c>&lt;style&gt;</c>,
    /// <c>&lt;script&gt;</c>, <c>&lt;link&gt;</c>, ...) into <c>&lt;head&gt;</c>, so a <c>&lt;style&gt;</c> block
    /// was dropped and the frame rendered as an empty <c>&lt;!--!--&gt;</c> container - the exact symptom seen in
    /// SpawnDev.AI's Home.razor. The renderer now parses through a <c>&lt;template&gt;</c>, whose "template"
    /// insertion mode keeps those elements as children in source order.
    /// </summary>
    public class StyleMarkupTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public StyleMarkupTests(IServiceProvider services) : base(services) { }

        /// <summary>
        /// Light-DOM mount (how SpawnDev.AI's Demo mounts, and why its <c>body</c>/<c>:root</c> rules need the
        /// document). The leading <c>&lt;style&gt;</c> node must render as a real element AND its rule must take
        /// effect on the sibling target (verified via <c>getComputedStyle</c>, the production observable).
        /// </summary>
        [RendererTest]
        public async Task StyleMarkupRendersAndAppliesInLightDomTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<StyleMarkupBox>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            using (var style = host.QuerySelector("style"))
                Assert.NotNull(style, "<style> element was dropped (parsed into <head> and lost)");
            using (var target = host.QuerySelector("[data-style-target]"))
                Assert.NotNull(target, "sibling element after the <style> did not render");

            Assert.Equal("rgb(7, 113, 219)", ComputedColor(host, "[data-style-target]"),
                "the <style> rule did not apply to the target element");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Shadow-root mount (how the browser extension isolates its UI on arbitrary host pages). The
        /// <c>&lt;style&gt;</c> scopes to the shadow root, so its rule must apply to the target inside that root.
        /// </summary>
        [RendererTest]
        public async Task StyleMarkupRendersAndAppliesInShadowRootTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<StyleMarkupBox>(host, new AttachShadowRootOptions { Mode = "open" });
            var r = NewRenderer(mappings);
            await r.Ready;

            using var shadow = host.ShadowRoot;
            Assert.NotNull(shadow, "host has no shadowRoot");
            using (var style = shadow!.QuerySelector("style"))
                Assert.NotNull(style, "<style> element was dropped inside the shadow root");
            using (var target = shadow!.QuerySelector("[data-style-target]"))
                Assert.NotNull(target, "sibling element after the <style> did not render in the shadow root");

            Assert.Equal("rgb(7, 113, 219)", ComputedColor(shadow, "[data-style-target]"),
                "the <style> rule did not apply to the target inside the shadow root");
            await Task.CompletedTask;
        }

        /// <summary>Resolves the computed <c>color</c> of the element matched by <paramref name="selector"/>.</summary>
        string ComputedColor(Node root, string selector)
        {
            using var el = QuerySelector(root, selector);
            Assert.NotNull(el, $"no element matched {selector}");
            using var window = JS.Get<Window>("window")!;
            using var computed = window.GetComputedStyle(el!);
            return computed.GetPropertyValue("color");
        }
    }
}
