# Prodexa

An open-source manufacturing data platform built with .NET 10, ASP.NET Core, EF Core and PostgreSQL.

Prodexa imports interconnected production records with source provenance and analyzes three manufacturing scenarios: overdue production, overlapping equipment downtime, and material consumption against versioned norms.

## Run the demo

Requires a .NET 10 or newer SDK, the .NET 10 runtime, Docker (running), and Python 3 for the demo verification script. The application targets `net10.0`; `global.json` prefers a 10.x SDK if present but allows a newer installed SDK (for example, 11.0.100) to build it.

Start the Aspire AppHost from the repository root:

```sh
dotnet run --project src/Prodexa.AppHost/Prodexa.AppHost.csproj
```

Aspire starts PostgreSQL 17 and the modular Prodexa API at `http://localhost:5080`. The development AppHost enables EF Core migrations on startup; running the API independently does not automatically migrate the database. A persistent Docker volume keeps local data between runs.

In another terminal, import the example dataset and verify all three scenarios:

```sh
python3 scripts/demo.py --url http://localhost:5080
```

The script loads `samples/demo-batch.json`, checks idempotent replay, and verifies the reports for **2026-11-01 12:00 (+05:00)**:

| Scenario | Expected result |
| --- | --- |
| UC01 — Production task J-01 | 60 accepted, 40 remaining, 120 minutes overdue |
| UC02 — Equipment EQ-01 | 60 minutes downtime, 75% availability |
| UC03 — Task J-02 / MAT-A | 115 kg consumed − 5 kg returned = 110 kg; +10 kg (+10%) against the norm |

Running the script again is safe: unchanged source revisions are skipped. To use another API address, supply `--url`.

**Note:** The API has no authentication yet. Run the demo only in a trusted local development environment.

## Architecture

`Prodexa.API` hosts an ASP.NET Core modular monolith. Production, Equipment, Materials and Importing are independently registered modules with their own controllers and services. `Prodexa.Core` contains manufacturing models; `Prodexa.Persistence` handles EF Core mappings and PostgreSQL migrations. `Prodexa.AppHost` orchestrates the API and its development database.

## Development

```sh
dotnet restore src/Prodexa.slnx
dotnet build src/Prodexa.slnx --configuration Release
dotnet test src/tests/Prodexa.Tests/Prodexa.Tests.csproj
```

Integration tests require `PRODEXA_TEST_CONNECTION_STRING` pointing to a dedicated PostgreSQL database. GitHub Actions supplies PostgreSQL automatically.

See [import formats](docs/importing.md) for the supported JSON and CSV schemas, and [configurable CSV mapping](docs/csv-mapping.md) to discover target fields, save source-specific mapping profiles, preview conversions and import arbitrary CSV columns using Swagger UI.

Apache AGE remains a proposed extension; the current implementation uses relational PostgreSQL storage.
