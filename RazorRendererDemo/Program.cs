using RazorRendererDemo;
using RazorRendererDemo.Services;
using SpawnDev;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using SpawnDev.SpawnJS.RazorRenderer;
using SpawnDev.SpawnJS.WebWorkers;

// .Net Wasm, unlike Blazor, does not come with a built-in dependency injection container.
// SpawnJSApp is a very minimal DI container that can be used when not using something else.
var builder = SpawnJSAppBuilder.CreateDefault(args);

// register SpawnJSRuntime
builder.Services.AddSpawnJSRuntime(out var JS);

Console.WriteLine($"{AppDomain.CurrentDomain.FriendlyName} {JS.GlobalScopeName} {JS.AppBaseUri}");

// register WebWorkerService
builder.Services.AddWebWorkerService();

// register the interactive Razor renderer (SpawnDomRenderer singleton)
builder.Services.AddRazorRenderer();

// Additional services
builder.Services.AddSingleton<TestService>();

// HTTPClient set to the app's base address 
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(JS.AppBaseUri) });

// SpawnJSRunAsync autostarts IBackgroundService and IAsyncBackgroundService services
// and can take a method that runs after all auto-starting services are started
await builder.Build().SpawnJSRunAsync(async (app) =>
{
    // Window scoped
    if (JS.GlobalScope == GlobalScope.Window)
    {
        var useShadowRoot = true;
        if (useShadowRoot)
        {
            // Mount App as an interactive Blazor root component, rendered to the real DOM by SpawnDomRenderer.
            // Mounting into an OPEN SHADOW ROOT proves the renderer runs isolated from the host page's DOM/CSS.
            var renderer = app.Services.GetRequiredService<SpawnDomRenderer>();
            using var document = JS.Get<Document>("document");
            var host = document!.CreateElement<HTMLDivElement>("div");
            host.Id = "shadow-host";
            document.Body!.Append(host);
            var shadow = host.AttachShadow(new AttachShadowRootOptions { Mode = "open" });
            await renderer.RenderComponentAsync<App>(shadow);
        }
        else
        {
            // Mount App as an interactive Blazor root component, rendered to the real DOM by SpawnDomRenderer.
            var renderer = app.Services.GetRequiredService<SpawnDomRenderer>();
            using var document = JS.Get<Document>("document");
            var host = document!.CreateElement<HTMLDivElement>("div");
            document.Body!.Append(host);
            await renderer.RenderComponentAsync<App>(host);
        }
    }
});
