var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17")
    .WithDataVolume("prodexa-postgres");

var database = postgres.AddDatabase("prodexa");

builder.AddDotnetProject("prodexa-api", "../Prodexa.API/Prodexa.API.csproj")
    .WithReference(database, connectionName: "Prodexa")
    .WaitFor(database)
    .WithHttpEndpoint(port: 5080)
    .WithExternalHttpEndpoints()
    .WithEnvironment("Prodexa__MigrateOnStartup", "true");

builder.Build().Run();
