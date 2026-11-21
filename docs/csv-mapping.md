# Configurable CSV imports

Prodexa can map columns from an arbitrary CSV into a supported manufacturing model without writing a new C# parser for each source. Mapping profiles are JSON documents saved in PostgreSQL.

## Browser and API workflow

Start the development AppHost with `dotnet run --project src/Prodexa.AppHost`, then open **http://localhost:5080/swagger** to test the HTTP endpoints.

1. **Browser only:** use the File API and a CSV parser to read column headers and the first 10–20 rows locally. The file is not uploaded during configuration.
2. `GET /api/import/targets` — list supported Prodexa models, fields, required flags, conversion types and enum values.
3. **Browser only:** let the user bind columns to target fields and assemble the mapping JSON. The target property name is the key; `column` is the source CSV header.
4. `POST /api/import/mappings/preview` — send the **unsaved mapping** and up to 20 locally parsed rows as JSON to validate conversion using the real backend engine. It does not write to PostgreSQL.
5. `POST /api/import/mappings` — validate and save the completed profile in PostgreSQL. `GET /api/import/mappings` and `GET /api/import/mappings/{id}` load existing definitions.
6. `POST /api/import/mappings/{id}/import?datasetId=packaging-2020` — upload the full CSV as multipart/form-data field `file` to populate the database. This endpoint has a 75 MB file limit.

The old server-side `POST /api/import/csv/inspect` endpoint has been **removed**. Reading a header and showing original sample rows is the web client's responsibility; the API never needs the entire file to build a mapping.

### Preview request (JSON)

The preview endpoint accepts a mapping definition even **before** it has been saved. For example:

```json
{
  "mapping": {
    "id": "equipment-from-csv",
    "name": "Import machine identifiers",
    "sourceSystem": "MES",
    "target": "Equipment",
    "fields": {
      "id": { "column": "machine_id" }
    }
  },
  "datasetId": "mes-2026-11",
  "rows": [
    { "machine_id": "EQ-01" },
    { "machine_id": "EQ-02" }
  ]
}
```

The server returns `rowsScanned`, `filteredRows` and `records` with canonical converted data. The sample must contain **1–20 rows**, and the request body is capped at **128 KiB**. The client should submit the **first consecutive data rows** if it wants row-number-derived preview IDs to match IDs created when the complete file is imported. A preview does not validate unseen rows.

## JSON mapping format

The example in [piade-downtime-mapping.json](../samples/piade-downtime-mapping.json) uses a single mapping for all machine downtime events:

```json
{
  "id": "piade-downtime-v1",
  "name": "PIADE machine downtime",
  "sourceSystem": "PIADE",
  "target": "Downtime",
  "sourceRevision": "1",
  "filters": {
    "type": [
      "downtime",
      "scheduled_downtime"
    ]
  },
  "fields": {
    "id": {
      "generate": "stable_id"
    },
    "equipmentId": {
      "column": "equipment_ID"
    },
    "startAt": {
      "column": "interval_start",
      "convertTo": "datetime_utc",
      "sourceFormat": "utc_naive"
    },
    "endAt": {
      "column": "end",
      "convertTo": "datetime_utc",
      "sourceFormat": "unix_seconds"
    },
    "reasonCode": {
      "column": "alarm"
    },
    "isPlanned": {
      "column": "type",
      "equals": "scheduled_downtime",
      "convertTo": "boolean"
    }
  }
}
```

The keys under `fields` are properties of the **target Prodexa model**. Thus `startAt` binds to `Downtime.StartAt`, while `column: "interval_start"` names the original CSV header. No separate `binding` field is required.

Supported field expressions:

- `column` — read a CSV column.
- `constant` — supply a constant string, number, boolean, or null.
- `generate: "stable_id"` — make a stable identifier using source system, profile, dataset ID and source row position.
- `convertTo` — explicit target type: `string`, `boolean`, `integer`, `decimal`, `enum`, or `datetime_utc`. It must match the destination property's type; otherwise it is inferred.
- `equals` — only for boolean destination fields. The result is `true` when the CSV string exactly matches the given value.
- `sourceFormat` — for timestamps only: `iso8601` (must include UTC offset), `utc_naive` (treat a timestamp without an offset as UTC), `unix_seconds`, or `unix_milliseconds`.
- `filters` — include only rows whose values appear in the configured allowed list.

Only one of `column`, `constant` or `generate` may be used per target field. All required fields must be mapped. Columns are case-sensitive. Invalid values, columns, target fields and enum names produce HTTP 400.

## Creating the referenced equipment

Importing `Downtime` requires the referenced equipment to exist. You can use another mapping profile for the **same CSV** to create the equipment first:

```json
{
  "id": "piade-equipment-v1",
  "name": "PIADE equipment",
  "sourceSystem": "PIADE",
  "target": "Equipment",
  "sourceRecordIdColumn": "equipment_ID",
  "sourceRevision": "1",
  "fields": {
    "id": {
      "column": "equipment_ID"
    }
  }
}
```

Importing this profile will skip identical repeated `equipment_ID` values through `sourceRecordIdColumn`. Then import the downtime profile with the same CSV. The underlying source revisions remain separate because record types are part of the provenance key.

## Idempotency and large files

The API derives a deterministic source ID from the `datasetId` and row position when `sourceRecordIdColumn` is omitted. Supply **the same datasetId** when replaying the same file. The first import persists `SourceRecord` entries. Replaying identical data skips previously imported records, while changing their content under the same source revision causes an HTTP 409 conflict. For row-number-based IDs, inserting or reordering source rows changes their identity; use a stable source ID column when available.

Large imports are processed in batches of 500 mapped rows. **Each batch commits independently.** If a late row fails validation, earlier batches remain committed; correct the data and retry the same dataset ID to safely skip already stored rows. The client-selected preview checks at most 20 rows and therefore does not guarantee validation of the entire file.

The importer does not infer missing production orders, downtime classifications or material norms. It only converts values explicitly supplied by the CSV and mapping definition.

## UI design

A future web client can implement the editor without transmitting the source CSV until the final import:

- Read and display the CSV header and sample using a client-side CSV parser (including quoted fields and encoding considerations).
- Ask `GET /api/import/targets` for available destination properties.
- Let the user select source columns, constants, generated IDs, type conversions and filters.
- Assemble the JSON mapping in the browser and submit it with up to 20 sample rows to `POST /api/import/mappings/preview`.
- Save the mapping with `POST /api/import/mappings`; only upload the complete file to `POST /api/import/mappings/{id}/import` when the user explicitly starts importing.
- For large CSV files, parse or sample in a Web Worker to keep the browser responsive.

Swagger provides this functionality as manual HTTP forms until a dedicated frontend is implemented. Swagger UI is enabled only in the development environment. The import API currently has no authentication, so it should not be exposed to untrusted networks.
