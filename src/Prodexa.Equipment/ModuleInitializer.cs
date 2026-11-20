using Microsoft.Extensions.DependencyInjection;
using Prodexa.Core.Resources;
using Prodexa.Equipment.Services;

namespace Prodexa.Equipment;

/// <summary>
/// Registers equipment analysis services with the application.
/// </summary>
public sealed class ModuleInitializer
{
    /// <summary>
    /// Adds equipment analysis services to the dependency injection container.
    /// </summary>
    public void AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DowntimeCalculator>();
        services.AddScoped<EquipmentAnalysisService>();
    }
}
