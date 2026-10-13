# JSON Extension

The JSON extension provides formatter capabilities for reading from and writing to JSON files. Read and write  both support string, number, and boolean property values, arrays, and hierarchical nested object structures. 

> **Note**: This is a File Format extension that is only used in combination with Binary Storage extensions. 

> **Note**: When specifying the JSON extension as the Source or Sink property in configuration, utilize the names listed below.

Supported storage sinks:
- File - **Json**
- Azure Blob Storage - **Json-AzureBlob**
- AWS S3 - **Json-AwsS3**
 
Supported storage sources:
- File - **Json**
- Azure Blob Storage - **Json-AzureBlob**
- AWS S3 - **Json-AwsS3**

## Settings

See storage extension documentation for any storage specific settings needed ([ex. File Storage](../../Interfaces/Cosmos.DataTransfer.Common/README.md)).

### Source

Source does not require any formatter specific settings.

```json
{
}
```

### Sink

Sink supports an optional `Indented` parameter (`false` by default) and an optional `IncludeNullFields` parameter (`false` by default) to control the formatting of the JSON output. Sink also supports an optional `BufferSizeMB` parameter (`200` by default) to constrain the in-memory stream buffer.

Additionally, an optional `ItemProgressFrequency` parameter (`1000` by default) controls how often item processing progress is logged during migration.

```json
{
    "Indented": true,
    "IncludeNullFields": true,
    "BufferSizeMB": 200,
    "ItemProgressFrequency": 1000
}
```

## Notes

### Multi-dimensional arrays

Multi-dimensional (nested) arrays are supported on write. This includes GeoJSON geometries such as `LineString` (2-D), `Polygon` (3-D), and `MultiPolygon` (4-D) `coordinates`. Documents that previously emitted `"System.Collections.Generic.List``1[System.Object]"` placeholders for these shapes now serialize correctly.

### Maximum nesting depth

To guard against pathological or recursively nested input (which would otherwise stack-overflow the host process), JSON serialization enforces a **maximum combined object + array nesting depth of 64**. Documents that exceed this limit are rejected with an `InvalidOperationException` whose message contains the phrase `nesting depth`.

This limit is well above any realistic document — GeoJSON `MultiPolygon` reaches depth 5, and typical nested business documents are under depth 10 — but if you hit it, it almost always indicates a corrupt or self-referential source document.
