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

```json
{
    "Indented": true,
    "IncludeNullFields": true,
    "BufferSizeMB": 200
}
```
