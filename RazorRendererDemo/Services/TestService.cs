using SpawnDev;
using SpawnDev.SpawnJS;

namespace RazorRendererDemo.Services
{
    public class TestService(SpawnJSRuntime JS) : IAsyncBackgroundService
    {
        Task? _ready = null;
        public Task Ready => _ready ??= InitAsync();

        async Task InitAsync()
        {
            Console.WriteLine($"TestService.InitAsync() {AppDomain.CurrentDomain.FriendlyName} {JS.GlobalScopeName} {JS.AppBaseUri}");
        }
    }
}
