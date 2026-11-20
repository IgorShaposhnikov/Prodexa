using Microsoft.Extensions.DependencyInjection;
using Prodexa.Materials.Services;

namespace Prodexa.Materials;

/// <summary>
/// Registers the material analytics module with dependency injection.
/// </summary>
public sealed class ModuleInitializer
{
    /// <summary>
    /// Adds the material consumption analysis service to the application.
    /// </summary>
    public void AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<MaterialConsumptionAnalysisService>();
    }
}
