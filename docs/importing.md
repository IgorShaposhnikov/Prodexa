# Manufacturing data import

Prodexa accepts versioned source records and preserves their origin alongside the stored manufacturing entities. The import is atomic: either every new record is saved, or none is.

## JSON batches

`POST /api/import/records` with `Content-Type: application/json`:

```json
{
  "sourceSystem": "MES",
  "records": [
    {
      "recordType": "Equipment",
      "sourceRecordId": "equipment-01",
      "sourceRevision": "1",
      "data": { "id": "EQ-01" }
    },
    {
      "recordType": "Downtime",
      "sourceRecordId": "downtime-01",
      "sourceRevision": "1",
      "sourceEventAt": "2026-11-01T07:50:00+05:00",
      "data": {
        "id": "D-01",
        "equipmentId": "EQ-01",
        "startAt": "2026-11-01T07:50:00+05:00",
        "endAt": "2026-11-01T08:20:00+05:00",
        "reasonCode": "Mechanical",
        "isPlanned": false
      }
    }
  ]
}
```

Supported record types: `PlanTask`, `PlanTaskRevision`, `Execution`, `Equipment`, `Downtime`, `MaterialDefinition`, `MaterialDefinitionVersion`, `MaterialLot`, `MaterialMovement`, `ConsumptionNorm`, and `PlanTaskNorm`. Enum values are strings. All related objects can be imported in the same batch; referenced records must exist already or be included in that batch.

A successful request returns the source system, `importedCount` and `skippedCount`. Replaying the same source system, record type, source ID, revision and payload skips the record. Reusing a source revision with different content or a canonical ID that already exists returns HTTP 409. Missing references or invalid records return HTTP 400 and roll back the batch.

Canonical records are immutable once imported. New plan and norm revisions require new canonical IDs. Each batch accepts 1–500 records and has a 1 MiB request limit.

## CSV event imports

CSV inputs use UTF-8, comma delimiters, a header row, and quoted fields where necessary. Supply the source system in the query parameter.

`POST /api/import/downtime-csv?sourceSystem=MES` with `Content-Type: text/csv`:

```csv
sourceRecordId,sourceRevision,id,equipmentId,startAt,endAt,reasonCode,isPlanned,comment
downtime-02,1,D-02,EQ-01,2026-11-01T08:10:00+05:00,2026-11-01T09:00:00+05:00,Mechanical,false,"Motor, inspection"
```

`POST /api/import/material-movements-csv?sourceSystem=MES` with `Content-Type: text/csv`:

```csv
sourceRecordId,sourceRevision,id,executionId,materialLotId,kind,quantity,unitId,occurredAt,outputQuality,reversesMovementId
output-01,1,MOV-01,EX-01,LOT-PROD,Output,50,piece,2026-11-01T09:00:00+05:00,Good,
consume-01,1,MOV-02,EX-01,LOT-MAT,Consume,115,kg,2026-11-01T09:00:00+05:00,,
return-01,1,MOV-03,EX-01,LOT-MAT,Return,5,kg,2026-11-01T09:30:00+05:00,,MOV-02
```

For material movements, create the referenced execution and material lots first. A return must reference the original consumption movement. Quantities use a dot as decimal separator. Timestamps should include their UTC offset.

Once imported, data can be analyzed through `GET /api/production/overdue-tasks`, `GET /api/equipment/{equipmentId}/downtime`, and `GET /api/materials/consumption-variance`.
