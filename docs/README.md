# Documentation

Prodexa currently runs as an ASP.NET Core modular monolith with PostgreSQL, EF Core migrations and local .NET Aspire orchestration. It implements three analytical scenarios, JSON/CSV ingestion, declarative CSV mapping, source provenance and idempotent replay. The browser workspace and Swagger provide access to the import API.

- [Quick start and reproducible scenarios](../README.md)
- [Import formats and constraints](importing.md)
- [CSV mapping profiles and browser workflow](csv-mapping.md)

The implemented relational model supports the current analytical scenarios. The conceptual model also identifies generic attributes, resource relationships and graph traversal as future work. Apache AGE has not been integrated; the graph storage and synchronization design remains open. The current prototype is intended for a trusted development environment.
