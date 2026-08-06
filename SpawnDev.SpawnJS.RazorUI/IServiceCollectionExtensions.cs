using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpawnDev.SpawnJS.RazorRenderer;

namespace SpawnDev.SpawnJS.RazorUI;

/// <summary>DI registration for RazorUI.</summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Registers RazorUI - the <see cref="RazorUITheme"/> singleton - and the underlying RazorRenderer it
    /// depends on. Safe to call alongside <c>AddRazorRenderer()</c>; both use TryAdd.
    /// </summary>
    public static IServiceCollection AddRazorUI(this IServiceCollection services)
    {
        services.AddRazorRenderer();
        services.TryAddSingleton<RazorUITheme>();
        return services;
    }
}
