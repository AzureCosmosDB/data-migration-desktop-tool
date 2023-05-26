# Parquet Extension (beta)

The Parquet extension provides formatter capabilities for reading from and writing to Parquet files. Read and write currently support flat object structures with string, boolean, datetime, and numeric property types. 

> **Note**: This is a File Format extension that is only used in combination with Binary Storage extensions. 

> **Note**: When specifying the Parquet extension as the Source or Sink property in configuration, utilize the names listed below.

Supported storage sinks:
- File - **Parquet(beta)**
- Azure Blob Storage - **Parquet-AzureBlob(beta)**
- AWS S3 - **Parquet-AwsS3(beta)**
 
Supported storage sources:
- File - **Parquet(beta)**

## Settings

The Parquet format does not currently include any settings. See storage extension documentation for any storage specific settings needed ([ex. File Storage](../../Interfaces/Cosmos.DataTransfer.Common/README.md)).