using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SpawnDev.SpawnJS.RazorRenderer
{
    /// <summary>
    /// SpawnDev.SpawnJS.RazorRenderer IServiceCollection extension methods.
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the <see cref="SpawnDomRenderer"/> singleton - an interactive Blazor renderer that runs in
        /// .NET WASM and applies render batches directly to the DOM via SpawnJS. One renderer hosts many
        /// root components; mount them with <see cref="SpawnDomRenderer.RenderComponentAsync{TComponent}"/>.
        /// </summary>
        public static IServiceCollection AddRazorRenderer(this IServiceCollection _this)
        {
            _this.TryAddSingleton<SpawnDomRenderer>();
            var existing = _this.FirstOrDefault(o => o.ServiceType == typeof(SpawnJSRootComponentMappingCollection));
            var ret = existing?.ImplementationInstance as SpawnJSRootComponentMappingCollection;
            if (ret == null)
            {
                var newRet = new SpawnJSRootComponentMappingCollection();
                _this.AddSingleton<SpawnJSRootComponentMappingCollection>(newRet);
            }
            return _this;
        }
    }
}
