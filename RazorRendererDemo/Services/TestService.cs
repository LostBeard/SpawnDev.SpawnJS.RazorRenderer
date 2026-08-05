using SpawnDev;
using SpawnDev.SpawnJS;
using System;
using System.Threading.Tasks;

namespace RazorRendererDemo.Services
{
    public class TestService(SpawnJSRuntime JS) : IAsyncBackgroundService
    {
        Task? _ready = null;
        public Task Ready => _ready ??= InitAsync();

        async Task InitAsync()
        {
            Console.WriteLine($"TestService.InitAsync() {JS.GlobalScopeName}");
        }
    }
}
