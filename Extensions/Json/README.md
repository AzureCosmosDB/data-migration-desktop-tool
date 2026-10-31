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

A source may contain a single JSON object or an array of objects, including on non-seekable HTTP/cloud streams and in compressed files. Arrays are read incrementally. A valid empty array (`[]`) is an empty source; an empty file or malformed JSON fails the migration (CLI exit code `1`).

Leading spaces, tabs, and line breaks are supported, including large amounts before the first JSON object or array. Format detection uses a fixed-size buffer and does not require temporary disk space. Reported JSON error locations remain relative to the original source.

Parse failures retain the JSON path, zero-based line number, and byte position reported by the JSON parser. The error log identifies the source type and stream number; the preceding source read log identifies the file or resource. Document contents are not dumped for diagnostics. Parsing still uses the default JSON rules (for example, no comments or trailing commas).

For errors in streamed arrays, use the line number and byte position to locate the error; the parser's JSON path can be less precise.

Sinks are not transactional: a source failure can leave partial output. Do not use that output as a completed migration.

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
