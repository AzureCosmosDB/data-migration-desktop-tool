# Parquet Extension

The Parquet extension provides formatter capabilities for reading from and writing to Parquet files. Read and write currently support flat object structures with string, boolean, datetime, and numeric property types. 

> **Note**: This is a File Format extension that is only used in combination with Binary Storage extensions. 

> **Note**: When specifying the Parquet extension as the Source or Sink property in configuration, utilize the names listed below.

## DateTimeOffset Handling

**Important**: `DateTimeOffset` values are automatically converted to UTC `DateTime` when writing to Parquet format. This conversion is necessary because Parquet.NET dropped support for `DateTimeOffset` in version 4.3.0 due to ambiguity issues (see [Parquet.NET release notes](https://github.com/aloneguid/parquet-dotnet/releases/tag/4.3.0)).

When migrating data with `DateTimeOffset` fields (such as the `Timestamp` property in Azure Table Storage entities):
- The absolute point in time is preserved by converting to UTC
- Timezone offset information is lost during conversion
- All timestamps are stored consistently in UTC format

This behavior enables migrations from sources like Azure Table Storage to Parquet, which would otherwise fail with a `NotSupportedException`.

Supported storage sinks:
- File - **Parquet**
- Azure Blob Storage - **Parquet-AzureBlob**
- AWS S3 - **Parquet-AwsS3**
 
Supported storage sources:
- File - **Parquet**
- Azure Blob Storage - **Parquet-AzureBlob**
- AWS S3 - **Parquet-AwsS3**

## Settings

The Parquet format supports an optional `ItemProgressFrequency` parameter (`1000` by default) that controls how often item processing progress is logged during migration.

```json
{
    "ItemProgressFrequency": 1000
}
```

See storage extension documentation for any storage specific settings needed ([ex. File Storage](../../Interfaces/Cosmos.DataTransfer.Common/README.md)).