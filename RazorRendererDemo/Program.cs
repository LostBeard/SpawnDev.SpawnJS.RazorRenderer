using RazorRendererDemo;
using RazorRendererDemo.Services;
using SpawnDev;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.RazorRenderer;
using SpawnDev.SpawnJS.RazorUI;
using SpawnDev.SpawnJS.WebWorkers;

// SpawnJSApp is a very minimal DI container that can be used when not using something else.
var builder = SpawnJSAppBuilder.CreateDefault(args, out var JS);

// Adds auto-generated App style sheet and auto-generated RCL stylesheet (fingerprinting and compression disabled)
builder.RootComponents.AddSharedStyleSheet(
    "RazorRendererDemo.styles.css",
    "SpawnDev.SpawnJS.RazorUI.bundle.scp.css");

// Add root components
builder.RootComponents.Add<App>();

// register WebWorkerService
builder.Services.AddWebWorkerService();

// register the interactive Razor renderer (SpawnDomRenderer singleton)
builder.Services.AddRazorRenderer();

// register RazorUI (themeable component library on top of the renderer)
builder.Services.AddRazorUI();

// Additional services
builder.Services.AddSingleton<TestService>();

// HTTPClient set to the app's base address 
builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(JS.AppBaseUri) });

// SpawnJSRunAsync autostarts IBackgroundService and IAsyncBackgroundService services
// and can take a method that runs after all auto-starting services are started
await builder.Build().RunAsync();
