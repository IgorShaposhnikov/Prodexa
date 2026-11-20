# Prodexa

A manufacturing data platform for modeling and analyzing production resources, tasks, materials, and operational events.

Prodexa is being developed with .NET 10, ASP.NET Core, EF Core, and a proposed PostgreSQL + Apache AGE storage model. The graph integration will be validated before it is adopted.

## Getting started

Requires the .NET 10 SDK.

```sh
dotnet restore src/Prodexa.slnx
dotnet build src/Prodexa.slnx --no-restore
```

## Repository

- `src/` — .NET solution and product projects.
- `docs/` — architecture notes and technical decisions.
- `.github/workflows/` — CI workflows.

Projects and dependencies are introduced when their functionality is implemented. The solution currently contains `Prodexa.Core`, which defines manufacturing models and rules without database dependencies.
