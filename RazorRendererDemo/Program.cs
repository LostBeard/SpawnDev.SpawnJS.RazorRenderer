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

// register WebWorkerService
builder.Services.AddWebWorkerService();

// register the interactive Razor renderer (SpawnDomRenderer singleton)
builder.Services.AddRazorRenderer();

// Additional services
builder.Services.AddSingleton<TestService>();

// build
var app = builder.Build();

// This starts IBackgroundService and IAsyncBackgroundService services as needed based on current global scope
await app.Services.StartBackgroundServices();

// Run the test suite in the window scope only. Workers load this same Program.cs; they must serve as
// workers, not re-run the suite. The Playwright TestRunner reads the READY/TEST/RESULTS console lines.
// `?filter=Name` in the url scopes the run. This mirrors the SpawnJS harness.
if (JS.GlobalScope == GlobalScope.Window)
{
    // Mount App as an interactive Blazor root component, rendered to the real DOM by SpawnDomRenderer.
    // Mounting into an OPEN SHADOW ROOT proves the renderer runs isolated from the host page's DOM/CSS.
    var renderer = app.Services.GetRequiredService<SpawnDomRenderer>();
    using var document = JS.Get<Document>("document");
    var host = document!.CreateElement<HTMLDivElement>("div");
    document.Body!.Append(host);
    var shadow = host.AttachShadow(new AttachShadowRootOptions { Mode = "open" });
    await renderer.RenderComponentAsync<App>(shadow);
}

// this keeps this app running until exited via a call to `SpawnJSApp.Exit()`
await app.RunAsync();
