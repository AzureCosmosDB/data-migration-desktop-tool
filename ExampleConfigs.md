# Example `migrationsettings.json` Files

## Multiple Cosmos-NoSQL Sinks (Different Accounts)

The tool supports writing to multiple different Cosmos DB accounts simultaneously using the `Operations` feature. This allows you to replicate data from one source to multiple destination accounts in a single execution.

```json
{
    "Source": "JSON",
    "Sink": "Cosmos-nosql",
    "SourceSettings": {
        "FilePath": "C:\\data\\sample-data.json"
    },
    "SinkSettings": {
        "PartitionKeyPath": "/id",
        "WriteMode": "UpsertStream"
    },
    "Operations": [
        {
            "SinkSettings": {
                "ConnectionString": "AccountEndpoint=https://account1.documents.azure.com:443/;AccountKey=<key1>;",
                "Database": "db1",
                "Container": "container1"
            }
        },
        {
            "SinkSettings": {
                "ConnectionString": "AccountEndpoint=https://account2.documents.azure.com:443/;AccountKey=<key2>;",
                "Database": "db2",
                "Container": "container2"
            }
        }
    ]
}
```

> **Note**: The tool creates separate CosmosClient instances for each operation's sink, allowing you to write to multiple different Cosmos DB accounts simultaneously. Each sink connection can have its own configuration including connection mode, proxy settings, and authentication method (connection string or RBAC).

## Cosmos-NoSQL to Cosmos-NoSQL (Different Accounts)

The tool supports simultaneous connections to two different Cosmos DB accounts, allowing you to migrate data directly from one account to another.

### Using Connection Strings

```json
{
    "Source": "Cosmos-nosql",
    "Sink": "Cosmos-nosql",
    "SourceSettings": {
        "ConnectionString": "AccountEndpoint=https://source-account.documents.azure.com:443/;AccountKey=<source-key>;",
        "Database": "sourceDatabase",
        "Container": "sourceContainer",
        "IncludeMetadataFields": false
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://destination-account.documents.azure.com:443/;AccountKey=<destination-key>;",
        "Database": "destinationDatabase",
        "Container": "destinationContainer",
        "PartitionKeyPath": "/id",
        "RecreateContainer": false,
        "WriteMode": "UpsertStream",
        "CreatedContainerMaxThroughput": 10000,
        "UseAutoscaleForCreatedContainer": true
    }
}
```

### Using RBAC Authentication (Passwordless)

```json
{
    "Source": "Cosmos-nosql",
    "Sink": "Cosmos-nosql",
    "SourceSettings": {
        "UseRbacAuth": true,
        "AccountEndpoint": "https://source-account.documents.azure.com:443/",
        "EnableInteractiveCredentials": true,
        "Database": "sourceDatabase",
        "Container": "sourceContainer",
        "IncludeMetadataFields": false
    },
    "SinkSettings": {
        "UseRbacAuth": true,
        "AccountEndpoint": "https://destination-account.documents.azure.com:443/",
        "EnableInteractiveCredentials": true,
        "Database": "destinationDatabase",
        "Container": "destinationContainer",
        "PartitionKeyPath": "/id",
        "WriteMode": "UpsertStream"
    }
}
```

> **Note**: The tool creates separate CosmosClient instances for the source and sink, allowing you to connect to different Cosmos DB accounts simultaneously. Each connection can have its own configuration including connection mode, proxy settings, and authentication method (connection string or RBAC).

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
