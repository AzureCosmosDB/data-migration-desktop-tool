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
        "Indented": true
    }
}
```

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
    "BlobName": "jan-alerts"
  },
  "Operations": [
  ]
}
```
