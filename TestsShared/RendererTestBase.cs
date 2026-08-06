using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorRenderer;

namespace RazorRendererTests
{
    /// <summary>
    /// Base for renderer tests. Each test gets its OWN <see cref="SpawnDomRenderer"/> built over its own
    /// <see cref="SpawnJSRootComponentMappingCollection"/> and its own host subtree, so no state leaks between
    /// tests. Awaiting <see cref="SpawnDomRenderer.Ready"/> drives the exact production startup path
    /// (attach shared style sheets, then render every mapping). Hosts and renderers created here are torn
    /// down in <see cref="Dispose"/>, which the runner calls after every test.
    /// </summary>
    public abstract class RendererTestBase : IDisposable
    {
        /// <summary>The live SpawnJS runtime.</summary>
        protected SpawnJSRuntime JS { get; }
        /// <summary>The app service provider (components resolve their DI services from it).</summary>
        protected IServiceProvider Services { get; }
        /// <summary>The document.</summary>
        protected Document Document { get; }

        readonly List<IDisposable> _disposables = new();
        readonly List<Element> _hosts = new();

        /// <summary>Constructed by the runner with the app service provider.</summary>
        protected RendererTestBase(IServiceProvider services)
        {
            Services = services;
            JS = SpawnJSRuntime.Instance ?? throw new InvalidOperationException("SpawnJSRuntime has not been created.");
            Document = JS.Get<Document>("document")!;
        }

        /// <summary>Creates a fresh host <c>&lt;div&gt;</c> appended to <c>&lt;body&gt;</c>, tracked for teardown.</summary>
        protected Element NewHost()
        {
            var host = Document.CreateElement("div");
            host.SetAttribute("data-test-host", Guid.NewGuid().ToString("N"));
            using var body = Document.Body;
            body!.Append(host);
            _hosts.Add(host);
            return host;
        }

        /// <summary>A fresh, empty root-component mapping collection.</summary>
        protected SpawnJSRootComponentMappingCollection NewMappings() => new SpawnJSRootComponentMappingCollection();

        /// <summary>
        /// A fresh renderer over <paramref name="mappings"/>, tracked for teardown. Await
        /// <see cref="SpawnDomRenderer.Ready"/> to attach shared style sheets and render the mappings.
        /// </summary>
        protected SpawnDomRenderer NewRenderer(SpawnJSRootComponentMappingCollection mappings)
        {
            var r = new SpawnDomRenderer(Services, JS, mappings);
            _disposables.Add(r);
            return r;
        }

        /// <summary>
        /// Polls <paramref name="predicate"/> until it is true or <paramref name="timeoutMs"/> elapses,
        /// yielding between polls (WASM is single threaded, so an async yield is what lets queued renders and
        /// event callbacks run). Returns the final predicate value.
        /// </summary>
        protected async Task<bool> WaitForAsync(Func<bool> predicate, int timeoutMs = 3000, int pollMs = 25)
        {
            var start = Environment.TickCount64;
            while (Environment.TickCount64 - start < timeoutMs)
            {
                if (predicate()) return true;
                await Task.Delay(pollMs);
            }
            return predicate();
        }

        /// <summary>Reads an element's <c>textContent</c> via a query, disposing the transient wrapper.</summary>
        protected string? TextOf(Node root, string selector)
        {
            using var el = QuerySelector(root, selector);
            return el?.TextContent;
        }

        /// <summary>querySelector against either an <see cref="Element"/> or a <see cref="ShadowRoot"/> host.</summary>
        protected Element? QuerySelector(Node root, string selector)
        {
            if (root is Element e) return e.QuerySelector(selector);
            if (root is ShadowRoot sr) return sr.QuerySelector(selector);
            throw new ArgumentException($"Unsupported query root: {root.GetType().Name}");
        }

        /// <summary>Disposes every renderer and removes every host this test created.</summary>
        public void Dispose()
        {
            foreach (var d in _disposables) { try { d.Dispose(); } catch { /* teardown best-effort */ } }
            foreach (var h in _hosts) { try { h.Remove(); } catch { } try { h.Dispose(); } catch { } }
        }
    }
}
