using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>
    /// Guards that elements under a <c>&lt;math&gt;</c> are created in the MathML namespace, across the same
    /// three arrival paths <see cref="SvgNamespaceTests"/> covers.
    /// <para>
    /// 🔴 SAME TRAP AS SVG: assert <c>namespaceURI</c>, never a selector. <c>querySelector("mi")</c> matches
    /// an <c>HTMLUnknownElement</c> named "mi" exactly as happily as a real MathML element, and an HTML
    /// "mi" renders as unstyled inline text rather than italic maths - present, wrong, and silent.
    /// </para>
    /// </summary>
    public class MathMLNamespaceTests : RendererTestBase
    {
        const string MathMLNs = "http://www.w3.org/1998/Math/MathML";
        const string SvgNs = "http://www.w3.org/2000/svg";

        /// <summary>Constructed by the runner.</summary>
        public MathMLNamespaceTests(IServiceProvider services) : base(services) { }

        async Task<Element> MountMathBoxAsync()
        {
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<MathBox>(host);
            var r = NewRenderer(mappings);
            await r.Ready;
            return host;
        }

        /// <summary>The &lt;math&gt; element itself opens the MathML namespace.</summary>
        [RendererTest]
        public async Task MathRootIsInMathMLNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountMathBoxAsync();

            using var math = host.QuerySelector("[data-math]");
            Assert.NotNull(math, "MathBox did not render a [data-math] element");
            Assert.Equal(MathMLNs, math!.NamespaceURI, "<math> namespaceURI");
        }

        /// <summary>A child carrying a dynamic attribute arrives as an element frame.</summary>
        [RendererTest]
        public async Task DynamicElementChildOfMathIsInMathMLNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountMathBoxAsync();

            using var mn = host.QuerySelector("[data-mn-dynamic]");
            Assert.NotNull(mn, "MathBox did not render the element-frame [data-mn-dynamic] child");
            Assert.Equal(MathMLNs, mn!.NamespaceURI, "element-frame child of <math> namespaceURI");
        }

        /// <summary>
        /// A fully static child is coalesced into a markup frame, so the parse context decides its
        /// namespace. Parsed through an HTML <c>&lt;template&gt;</c> it would be an HTMLUnknownElement.
        /// </summary>
        [RendererTest]
        public async Task StaticMarkupChildOfMathIsInMathMLNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountMathBoxAsync();

            using var mi = host.QuerySelector("[data-mi-static]");
            Assert.NotNull(mi, "MathBox did not render the markup-frame [data-mi-static] child");
            Assert.Equal(MathMLNs, mi!.NamespaceURI, "markup-frame child of <math> namespaceURI");
        }

        /// <summary>
        /// An <c>&lt;svg&gt;</c> nested inside a <c>&lt;math&gt;</c> re-opens the SVG namespace, and its own
        /// children follow it rather than the enclosing MathML. This is the ordering case: Blazor tests SVG
        /// before MathML, so whichever namespace the tag names opens must win over the inherited one.
        /// </summary>
        [RendererTest]
        public async Task SvgNestedInMathReopensSvgNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountMathBoxAsync();

            using (var svg = host.QuerySelector("[data-svg-in-math]"))
            {
                Assert.NotNull(svg, "MathBox did not render the nested [data-svg-in-math]");
                Assert.Equal(SvgNs, svg!.NamespaceURI, "<svg> inside <math> namespaceURI");
            }
            using var circle = host.QuerySelector("[data-nested]");
            Assert.NotNull(circle, "nested svg did not render its circle");
            Assert.Equal(SvgNs, circle!.NamespaceURI, "child of an <svg> nested in <math> namespaceURI");
        }
    }
}
