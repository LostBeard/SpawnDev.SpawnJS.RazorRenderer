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
            /// Root components that RazorRenderer will render when started in a browser Window global scope.<br/>
            /// RazorRenderer service is registered if not already registered.
            /// </summary>
            public SpawnJSRootComponentMappingCollection RootComponents
            {
                get
                {   
                    builder.Services.AddRazorRenderer(out var rootComponentMappings);
                    return rootComponentMappings;
                }
            }
        }
    }
}
