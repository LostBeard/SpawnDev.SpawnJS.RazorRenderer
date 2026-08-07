using SpawnDev;
using SpawnDev.SpawnJS;

namespace RazorRendererDemo.Services
{
    public class AppService(SpawnJSRuntime JS) : IAsyncBackgroundService
    {
        Task? _ready = null;
        public Task Ready => _ready ??= InitAsync();

        async Task InitAsync()
        {
            Console.WriteLine($"AppService.InitAsync() {AppDomain.CurrentDomain.FriendlyName} {JS.GlobalScopeName} {JS.AppBaseUri}");
        }
    }
}
