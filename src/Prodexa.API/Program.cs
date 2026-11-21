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

builder.Services.AddOpenApi();

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

// Aspire enables schema initialization only for the local demo environment.
if (app.Configuration.GetValue<bool>("Prodexa:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<ProdexaDbContext>();
    await context.Database.MigrateAsync();
}

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Prodexa API v1");
    });
}

app.Run();

public partial class Program
{
}
