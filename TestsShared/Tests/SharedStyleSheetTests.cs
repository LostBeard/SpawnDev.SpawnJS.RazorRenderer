using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorRenderer;

namespace RazorRendererTests
{
    /// <summary>
    /// Shared style sheets attach into shadow roots at startup, repoint when their href changes, and - the
    /// regression this guards - actually remove their <c>&lt;link&gt;</c> when the sheet is removed, even for
    /// sheets registered through the builder / ctor <c>TryAdd</c> path.
    /// </summary>
    public class SharedStyleSheetTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public SharedStyleSheetTests(IServiceProvider services) : base(services) { }

        const string SheetName = "test-shared.css";

        // Mounts a component into a shadow root with one collection-level shared style sheet, driving the
        // exact production path: renderer ctor TryAdd of mappings.StyleSheets, then RenderMapping attach.
        async Task<(SpawnDomRenderer Renderer, ShadowRoot Shadow)> MountWithSharedSheetAsync()
        {
            var host = NewHost();
            var mappings = NewMappings();
            mappings.AddSharedStyleSheet(SheetName);
            mappings.Add<Counter>(host, new AttachShadowRootOptions { Mode = "open" });
            var r = NewRenderer(mappings);
            await r.Ready;
            return (r, host.ShadowRoot!);
        }

        /// <summary>The shared sheet is attached as a <c>&lt;link&gt;</c> inside the shadow root at startup.</summary>
        [RendererTest]
        public async Task SharedSheetAttachedToShadowRootTest()
        {
            HostCapabilities.RequireBrowser();
            var (r, shadow) = await MountWithSharedSheetAsync();
            using (shadow)
            {
                var sheet = r.GetSharedStyleSheet(SheetName);
                Assert.NotNull(sheet, "shared style sheet was not registered on the renderer");
                using var link = r.GetStyleSheet(shadow, sheet!.Href);
                Assert.NotNull(link, "shared style sheet <link> was not attached into the shadow root");
            }
        }

        /// <summary>
        /// REGRESSION: a sheet registered via the builder / ctor <c>TryAdd</c> path must remove its
        /// <c>&lt;link&gt;</c> on <see cref="SharedStyleSheet.Remove"/>. Before the fix the OnStyleSheetRemoved
        /// event was unsubscribed on that path, so the link lingered.
        /// </summary>
        [RendererTest]
        public async Task RemoveSharedSheetRemovesLinkTest()
        {
            HostCapabilities.RequireBrowser();
            var (r, shadow) = await MountWithSharedSheetAsync();
            using (shadow)
            {
                var sheet = r.GetSharedStyleSheet(SheetName)!;
                using (var present = r.GetStyleSheet(shadow, sheet.Href))
                    Assert.NotNull(present, "precondition: link should be present before removal");

                sheet.Remove();

                var gone = await WaitForAsync(() =>
                {
                    using var link = r.GetStyleSheet(shadow, sheet.Href);
                    return link == null;
                });
                Assert.True(gone, "Remove() did not pull the shared style sheet <link> from the shadow root");
            }
        }

        /// <summary>Changing <see cref="SharedStyleSheet.Href"/> repoints the attached <c>&lt;link&gt;</c>.</summary>
        [RendererTest]
        public async Task RepointSharedSheetUpdatesLinkTest()
        {
            HostCapabilities.RequireBrowser();
            var (r, shadow) = await MountWithSharedSheetAsync();
            using (shadow)
            {
                var sheet = r.GetSharedStyleSheet(SheetName)!;
                var oldHref = sheet.Href;
                var newHref = new Uri(new Uri(JS.AppBaseUri), "test-shared-2.css").ToString();

                sheet.Href = newHref;

                var repointed = await WaitForAsync(() =>
                {
                    using var oldLink = r.GetStyleSheet(shadow, oldHref);
                    using var newLink = r.GetStyleSheet(shadow, newHref);
                    return oldLink == null && newLink != null;
                });
                Assert.True(repointed, "changing SharedStyleSheet.Href did not repoint the <link> in the shadow root");
            }
        }
    }
}
