using Microsoft.Extensions.DependencyInjection;
using Prodexa.Production.Services;

namespace Prodexa.Production;

/// <summary>
/// Registers the services provided by the production module.
/// </summary>
public sealed class ModuleInitializer
{
    /// <summary>
    /// Adds production services to the application dependency injection container.
    /// </summary>
    public void AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ProductionTaskService>();
        services.AddScoped<ProductionOverdueService>();
    }
}
