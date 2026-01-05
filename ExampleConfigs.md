# Example `migrationsettings.json` Files

## JSON to Cosmos-NoSQL

```json
{
    "Source": "json",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "FilePath": "https://mytestfiles.local/sales-data.json"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://...",
        "Database": "myDb",
        "Container": "myContainer",
        "PartitionKeyPath": "/id",
        "RecreateContainer": true,
        "WriteMode": "Insert",
        "CreatedContainerMaxThroughput": 5000,
        "IsServerlessAccount": false
    }
}
```

## Cosmos-NoSQL to JSON

```json
{
    "Source": "Cosmos-NoSql",
    "Sink": "JSON",
    "SourceSettings":
    {
        "ConnectionString": "AccountEndpoint=https://...",
        "Database":"cosmicworks",
        "Container":"customers",
        "IncludeMetadataFields": true
    },
    "SinkSettings":
    {
        "FilePath": "c:\\data\\cosmicworks\\customers.json",
        "Indented": true,
        "ItemProgressFrequency": 1000
    }
}
```

## Cosmos-NoSQL to MongoDB (with custom _id mapping)

```json
{
    "Source": "Cosmos-NoSql",
    "Sink": "MongoDB",
    "SourceSettings": {
        "ConnectionString": "AccountEndpoint=https://...",
        "Database": "cosmicworks",
        "Container": "baskets"
    },
    "SinkSettings": {
        "ConnectionString": "mongodb://localhost:27017",
        "DatabaseName": "mydb",
        "Collection": "baskets",
        "IdFieldName": "id"
    }
}
```

> **Note**: The `IdFieldName` parameter specifies which field from the source should be mapped to MongoDB's `_id` field. In this example, the `id` field from Cosmos will be used as the `_id` in MongoDB while also keeping the original `id` field in the document.

## MongoDB to Cosmos-NoSQL

```json
{
    "Source": "mongodb",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "ConnectionString": "mongodb://...",
        "DatabaseName": "sales",
        "Collection": "person"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://...",
        "Database": "users",
        "Container": "migrated",
        "PartitionKeyPath": "/id",
        "ConnectionMode": "Direct",
        "WriteMode": "UpsertStream",
        "CreatedContainerMaxThroughput": 8000,
        "UseAutoscaleForCreatedContainer": false
    }
}
```

## MongoDB Legacy (Wire v2) to Cosmos-NoSQL

```json
{
    "Source": "MongoDB-Legacy (Wire v2)",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "ConnectionString": "******mycluster.documents.azure.com:10255/?ssl=true",
        "DatabaseName": "sales",
        "Collection": "person"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://...",
        "Database": "users",
        "Container": "migrated",
        "PartitionKeyPath": "/id",
        "ConnectionMode": "Direct",
        "WriteMode": "UpsertStream",
        "CreatedContainerMaxThroughput": 8000,
        "UseAutoscaleForCreatedContainer": false
    }
}
```

## SqlServer to AzureTableAPI

```json
{
    "Source": "SqlServer",
    "Sink": "AzureTableApi",
    "SourceSettings": {
        "ConnectionString": "Server=...",
        "QueryText": "SELECT Id, Date, Amount FROM dbo.Payments WHERE Status = 'open'"
    },
    "SinkSettings": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
        "Table": "payments",
        "RowKeyFieldName": "Id"
    }
}
```

## AzureTableAPI to JSON (with DateTime Filter)

```json
{
    "Source": "AzureTableAPI",
    "Sink": "JSON",
    "SourceSettings": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<storage-account-name>;AccountKey=<key>;EndpointSuffix=core.windows.net",
        "Table": "SourceTable1",
        "PartitionKeyFieldName": "PartitionKey",
        "RowKeyFieldName": "RowKey",
        "QueryFilter": "Timestamp ge datetime\u00272023-05-15T03:30:32.663Z\u0027"
    },
    "SinkSettings": {
        "FilePath": "D:\\output\\filtered-data.json",
        "Indented": true
    }
}
```

> **Note**: When using DateTime filters in the `QueryFilter` property, single quotes around the datetime value must be JSON-escaped as `\u0027`. The datetime must be in ISO 8601 format with the `datetime` prefix.

## Cosmos-NoSQL to SqlServer

```json
{
    "Source": "cosmos-nosql",
    "Sink": "sqlserver",
    "SourceSettings":
    {
        "ConnectionString": "AccountEndpoint=https://...",
        "Database":"operations",
        "Container":"alerts",
        "PartitionKeyValue": "jan",
        "Query": "SELECT a.name, a.description, a.count, a.id, a.isSet FROM a"
    },
    "SinkSettings":
    {
        "ConnectionString": "Server=...",
        "TableName": "Import",
        "ColumnMappings": [
            {
                "ColumnName": "Name"
            },
            {
                "ColumnName": "Description"
            },
            {
                "ColumnName": "Count",
                "SourceFieldName": "number"
            },
            {
                "ColumnName": "Id"
            },
            {
                "ColumnName": "IsSet",
                "AllowNull": false,
                "DefaultValue": false
            }
        ]
    }
}
```

## Cosmos-NoSQL to Json-AzureBlob (Using RBAC)

```json
{
  "Source": "Cosmos-nosql",
  "Sink": "Json-AzureBlob",
  "SourceSettings": {
    "UseRbacAuth": true,
    "Database": "operations",
    "Container": "alerts",
    "PartitionKeyValue": "jan",
    "AccountEndpoint": "https://<databaseaccount>.documents.azure.com",
    "EnableInteractiveCredentials": true,
    "IncludeMetadataFields": false,
    "Query": "SELECT a.name, a.description, a.count, a.id, a.isSet FROM a"
  },
  "SinkSettings": {
    "UseRbacAuth": true,
    "ContainerName": "operations-archive",
    "AccountEndpoint": "https://<storage-account>.blob.core.windows.net",
    "EnableInteractiveCredentials": true,
    "BlobName": "jan-alerts",
    "ItemProgressFrequency": 1000
  },
  "Operations": [
  ]
}
```

## JSON to Cosmos-NoSQL (Using Authenticated Proxy)

```json
{
    "Source": "json",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "FilePath": "c:\\data\\sales-data.json"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://...",
        "Database": "myDb",
        "Container": "myContainer",
        "PartitionKeyPath": "/id",
        "WriteMode": "Insert",
        "WebProxy": "http://yourproxy.server.com/",
        "UseDefaultProxyCredentials": true,
        "UseDefaultCredentials": true,
        "PreAuthenticate": true
    }
}
```
