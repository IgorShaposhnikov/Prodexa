using Microsoft.Extensions.DependencyInjection;
using Prodexa.Importing.Services;

namespace Prodexa.Importing;

/// <summary>
/// Registers manufacturing data import capabilities.
/// </summary>
public sealed class ModuleInitializer
{
    /// <summary>
    /// Adds import processing and CSV parsing to the application's dependency injection container.
    /// </summary>
    public void AddServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ManufacturingImportService>();
        services.AddSingleton<CsvImportParser>();
        services.AddSingleton<CsvTargetCatalog>();
        services.AddSingleton<CsvMappingEngine>();
        services.AddScoped<CsvMappingService>();
    }
}
