using Microsoft.Extensions.DependencyInjection;

namespace SpawnDev.SpawnJS.RazorRenderer
{
    /// <summary>
    /// Adds extenions methods to SpawnJSAppbuilder
    /// </summary>
    public static class SpawnJSAppBuilderExtensions
    {
        extension(SpawnJSAppBuilder builder)
        {
            /// <summary>
            /// Root components that RazorRenderer will render when started in a browser Window global scope
            /// </summary>
            public SpawnJSRootComponentMappingCollection RootComponents
            {
                get
                {
                    var existing = builder.Services.FirstOrDefault(o => o.ServiceType == typeof(SpawnJSRootComponentMappingCollection));
                    var ret = existing?.ImplementationInstance as SpawnJSRootComponentMappingCollection;
                    if (ret != null) return ret;
                    var newRet = new SpawnJSRootComponentMappingCollection();
                    builder.Services.AddSingleton<SpawnJSRootComponentMappingCollection>(newRet);
                    return newRet;
                }
            }
        }
    }
}
