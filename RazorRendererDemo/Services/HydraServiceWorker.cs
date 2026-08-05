using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.WebWorkers;

namespace RazorRendererDemo.Services
{
    // Test ServiceWorkerEventHandler for proving the event-holder -> .Net drain round-trip
    // in the classic (Rollup) bundle. Registered to auto-start in the ServiceWorker scope only.
    // The distinctive log lines let the CDP test confirm that events fired during the async
    // .Net boot (held by the event-holder) are replayed to .Net after it starts.
    public class RazorRendererDemoServiceWorker : ServiceWorkerEventHandler
    {
        public RazorRendererDemoServiceWorker(SpawnJSRuntime js) : base(js) { }

        protected override Task OnInitializedAsync()
        {
            Console.WriteLine($"RazorRendererDemoServiceWorker OnInitializedAsync {JS.GlobalScopeName}");
            return Task.CompletedTask;
        }

        protected override Task ServiceWorker_OnInstallAsync(ExtendableEvent e)
        {
            // If this fires, the install event that occurred during the async .Net boot was
            // held by the event-holder and successfully replayed to .Net (missed-event drain works).
            Console.WriteLine("RazorRendererDemoServiceWorker ServiceWorker_OnInstallAsync (held install replayed)");
            return Task.CompletedTask;
        }

        protected override Task ServiceWorker_OnActivateAsync(ExtendableEvent e)
        {
            Console.WriteLine("RazorRendererDemoServiceWorker ServiceWorker_OnActivateAsync");
            return Task.CompletedTask;
        }

        protected override async Task<Response> ServiceWorker_OnFetchAsync(FetchEvent e)
        {
            Console.WriteLine($"RazorRendererDemoServiceWorker ServiceWorker_OnFetchAsync {e.Request.Method} {e.Request.Url}");
            try
            {
                return await JS.Fetch(e.Request);
            }
            catch
            {
                return Response.Error();
            }
        }

        protected override Task ServiceWorker_OnMessageAsync(ExtendableMessageEvent e)
        {
            Console.WriteLine("RazorRendererDemoServiceWorker ServiceWorker_OnMessageAsync");
            return Task.CompletedTask;
        }
    }
}
