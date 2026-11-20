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

builder.Services.AddControllers().AddApplicationPart(typeof(Prodexa.Production.ModuleInitializer).Assembly);

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program
{
}
