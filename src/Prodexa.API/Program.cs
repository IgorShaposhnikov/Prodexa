using Microsoft.EntityFrameworkCore;
using Prodexa.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProdexaDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Prodexa");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'Prodexa' is not configured.");
    }

    options.UseNpgsql(connectionString);
});

var productionModule = new Prodexa.Production.ModuleInitializer();
productionModule.AddServices(builder.Services);

var equipmentModule = new Prodexa.Equipment.ModuleInitializer();
equipmentModule.AddServices(builder.Services);

var materialsModule = new Prodexa.Materials.ModuleInitializer();
materialsModule.AddServices(builder.Services);

var importingModule = new Prodexa.Importing.ModuleInitializer();
importingModule.AddServices(builder.Services);

builder.Services.AddControllers()
    .AddApplicationPart(typeof(Prodexa.Production.ModuleInitializer).Assembly)
    .AddApplicationPart(typeof(Prodexa.Equipment.ModuleInitializer).Assembly)
    .AddApplicationPart(typeof(Prodexa.Materials.ModuleInitializer).Assembly)
    .AddApplicationPart(typeof(Prodexa.Importing.ModuleInitializer).Assembly);

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program
{
}
