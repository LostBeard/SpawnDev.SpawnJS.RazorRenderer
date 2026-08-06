using RazorRendererTests;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.RazorRenderer;
using SpawnDev.SpawnJS.RazorUI;

// The RazorRenderer unit-test host. It runs in a real browser (via `dotnet run` on the WASM SDK dev host)
// and its console output is the machine-readable TEST:/RESULTS: stream the Playwright TestRunner parses.

var builder = SpawnJSAppBuilder.CreateDefault(args, out var JS);

// The renderer and RazorUI theme service the tests exercise. No root components are registered here - each
// test builds its own renderer + host subtree - so startup renders nothing and stays out of the tests' way.
builder.Services.AddRazorRenderer();
builder.Services.AddRazorUI();

// RunAsync(whenReady) starts background services first (constructing the renderer + theme, awaiting Ready),
// THEN runs whenReady with the built service provider, so the suite has a fully initialized container.
await builder.Build().RunAsync(async app =>
{
    await TestSuiteRunner.RunAllAsync(app.Services, TestSuiteRunner.FilterFromLocation());
});
