using RazorRendererTests.Components;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>
    /// Markup (<c>MarkupString</c>) rendering sets a <c>&lt;template&gt;</c>'s <c>innerHTML</c>, a Trusted Types
    /// injection sink. On a page enforcing <c>require-trusted-types-for 'script'</c> (e.g. YouTube, Gmail) the
    /// plain-string assignment throws <c>TypeError: This document requires 'TrustedHTML'</c>, which used to
    /// abort the render batch mid-mutation and corrupt the shadow DOM. The renderer now routes markup through a
    /// Trusted Type policy (<see cref="SpawnDomRenderer"/>.SetMarkup).
    /// <para>
    /// Chrome exposes <c>window.trustedTypes</c> even without CSP enforcement, so these tests drive the exact
    /// production path - policy &#8594; <c>CreateHTML</c> &#8594; template <c>SetInnerHTML(TrustedHTML)</c> - in
    /// a real browser. (Active enforcement only ADDS rejection of the plain-string path; the TrustedHTML path
    /// is accepted identically with or without it.)
    /// </para>
    /// </summary>
    public class TrustedTypesTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public TrustedTypesTests(IServiceProvider services) : base(services) { }

        /// <summary>
        /// Raw markup renders into a shadow root through the Trusted Types-safe parse path: all top-level and
        /// nested markup nodes appear with their text. This is the render-level reproduction of the YouTube
        /// failure (before the fix, this threw inside DOMParser on a TT-enforcing page and left the tree
        /// half-built).
        /// </summary>
        [RendererTest]
        public async Task MarkupRendersThroughTrustedTypesPathTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<MarkupBox>(host, new AttachShadowRootOptions { Mode = "open" });
            var r = NewRenderer(mappings);
            await r.Ready;

            using var shadow = host.ShadowRoot;
            Assert.NotNull(shadow, "host has no shadowRoot");
            using (var a = shadow!.QuerySelector("[data-mk-a]"))
                Assert.NotNull(a, "first top-level markup node did not render");
            using (var b = shadow!.QuerySelector("[data-mk-b]"))
                Assert.NotNull(b, "second top-level markup node did not render");
            Assert.Equal("A", TextOf(shadow, "[data-mk-a]"), "first markup node text");
            Assert.Equal("!", TextOf(shadow, "[data-mk-em]"), "nested markup node text");
        }

        /// <summary>
        /// The new SpawnJS Trusted Types wrappers work end to end: a policy approves an HTML string, the
        /// factory recognises the produced value as TrustedHTML, and <c>DOMParser</c> parses that TrustedHTML
        /// to the expected DOM through the new typed overload.
        /// </summary>
        [RendererTest]
        public async Task TrustedTypePolicyParsesHtmlTest()
        {
            HostCapabilities.RequireBrowser();
            using var factory = JS.Get<TrustedTypePolicyFactory?>("trustedTypes");
            if (factory is null) throw new SkipTestException("Trusted Types not supported in this browser");

            using var createHtml = Callback.Create<string, string>(s => s);
            using var policy = factory.CreatePolicy("spawndev-razorrenderer-test",
                new TrustedTypePolicyOptions { CreateHTML = createHtml });

            const string markup = "<b class=\"tt\" data-tt>hi <i data-tt-i>there</i></b>";
            using var trusted = policy.CreateHTML(markup);
            Assert.True(factory.IsHTML(trusted), "factory did not recognise the produced value as TrustedHTML");

            using var parser = new DOMParser();
            using var doc = parser.ParseFromString(trusted, "text/html");
            using var body = doc.Body!;
            using (var b = body.QuerySelector("[data-tt]"))
                Assert.NotNull(b, "TrustedHTML parse did not produce the expected element");
            using (var i = body.QuerySelector("[data-tt-i]"))
                Assert.NotNull(i, "TrustedHTML parse did not produce the nested element");
            Assert.Equal("there", TextOf(body, "[data-tt-i]"), "nested element text");

            await Task.CompletedTask;
        }
    }
}
