using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorUI;

namespace RazorRendererTests
{
    /// <summary>
    /// Guards that elements under an <c>&lt;svg&gt;</c> are created in the SVG namespace.
    /// <para>
    /// 🔴 ASSERT ON <c>namespaceURI</c>, NEVER ON A SELECTOR. A CSS type selector matches an element's
    /// LOCAL NAME, so <c>querySelector("polygon")</c> matches an <c>HTMLUnknownElement</c> named
    /// "polygon" exactly as happily as a real <c>SVGPolygonElement</c>. An HTML element named "polygon"
    /// is legal, inherits the CSS aimed at it, and reports zero geometry - markup and styles look
    /// correct in devtools and NOTHING is drawn. That is precisely the state
    /// <c>UiIconTests.IconRendersRequestedShapeTest</c> passed in, labelled "SVG child namespace
    /// working", for the entire time the defect was live.
    /// </para>
    /// </summary>
    public class SvgNamespaceTests : RendererTestBase
    {
        const string SvgNs = "http://www.w3.org/2000/svg";
        const string HtmlNs = "http://www.w3.org/1999/xhtml";

        /// <summary>Constructed by the runner.</summary>
        public SvgNamespaceTests(IServiceProvider services) : base(services) { }

        async Task<Element> MountSvgBoxAsync()
        {
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<SvgBox>(host);
            var r = NewRenderer(mappings);
            await r.Ready;
            return host;
        }

        /// <summary>The &lt;svg&gt; element itself opens the SVG namespace.</summary>
        [RendererTest]
        public async Task SvgRootIsInSvgNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountSvgBoxAsync();

            using var svg = host.QuerySelector("[data-svg]");
            Assert.NotNull(svg, "SvgBox did not render an [data-svg] element");
            Assert.Equal(SvgNs, svg!.NamespaceURI, "<svg> namespaceURI");
        }

        /// <summary>
        /// A child carrying a dynamic attribute arrives as an ELEMENT frame and is created by the renderer.
        /// Guards the inherited <c>LogicalElement.IsSvg</c> flag.
        /// </summary>
        [RendererTest]
        public async Task DynamicElementChildOfSvgIsInSvgNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountSvgBoxAsync();

            using var circle = host.QuerySelector("[data-dynamic]");
            Assert.NotNull(circle, "SvgBox did not render the element-frame [data-dynamic] child");
            Assert.Equal(SvgNs, circle!.NamespaceURI, "element-frame child of <svg> namespaceURI");
        }

        /// <summary>
        /// A fully static child is coalesced into a MARKUP frame, so the renderer parses a raw string and
        /// whatever it parses with decides the namespace. Parsing SVG markup through an HTML
        /// <c>&lt;template&gt;</c> yields HTML elements with SVG tag names, which draw nothing.
        /// </summary>
        [RendererTest]
        public async Task StaticMarkupChildOfSvgIsInSvgNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountSvgBoxAsync();

            using var poly = host.QuerySelector("[data-static]");
            Assert.NotNull(poly, "SvgBox did not render the markup-frame [data-static] child");
            Assert.Equal(SvgNs, poly!.NamespaceURI, "markup-frame child of <svg> namespaceURI");
        }

        /// <summary>
        /// <c>&lt;foreignObject&gt;</c> is the one SVG element whose content is HTML again. Its own node stays
        /// in the SVG namespace; what it contains must not.
        /// </summary>
        [RendererTest]
        public async Task ForeignObjectReEntersHtmlNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = await MountSvgBoxAsync();

            using (var fo = host.QuerySelector("[data-fo]"))
            {
                Assert.NotNull(fo, "SvgBox did not render a [data-fo] element");
                Assert.Equal(SvgNs, fo!.NamespaceURI, "<foreignObject> itself namespaceURI");
            }
            using var inner = host.QuerySelector("[data-inner]");
            Assert.NotNull(inner, "foreignObject content did not render");
            Assert.Equal(HtmlNs, inner!.NamespaceURI, "content of <foreignObject> namespaceURI");
        }

        /// <summary>
        /// The production component, on the real symptom: a <see cref="UiIcon"/>'s geometry must be SVG.
        /// Captain, on the avatar this broke: "I can use devtools to see the svg there.. but nothing is
        /// actually visible. Just an empty borderless box."
        /// </summary>
        [RendererTest]
        public async Task UiIconGeometryIsInSvgNamespaceTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<UiIcon>(host, new Dictionary<string, object?> { ["Name"] = "play" });
            var r = NewRenderer(mappings);
            await r.Ready;

            using (var svg = host.QuerySelector(".ui-icon"))
            {
                Assert.NotNull(svg, "UiIcon did not render an svg.ui-icon");
                Assert.Equal(SvgNs, svg!.NamespaceURI, "UiIcon <svg> namespaceURI");
            }
            using var poly = host.QuerySelector(".ui-icon polygon");
            Assert.NotNull(poly, "play icon should render a <polygon>");
            Assert.Equal(SvgNs, poly!.NamespaceURI, "UiIcon <polygon> namespaceURI");
        }
    }
}
