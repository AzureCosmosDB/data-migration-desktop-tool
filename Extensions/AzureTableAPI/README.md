# AzureTableAPI Extension

This extension is built to facilitate both a data Source and Sink for the migration tool to be able to read and write to Azure Table API. This covers both the Azure Storage Table API and Azure Cosmos DB Table API, since they both adhere to the same API spec.

> **Note**: When specifying the AzureTableAPI extension as the Source or Sink property in configuration, utilize the name **AzureTableAPI**.
> 
## Configuration Settings

The AzureTableAPI has a couple required and optional settings for configuring the connection to the Table API you are connecting to. This applies to both Azure Storage Table API and Azure Cosmos DB Table API.

The following are the required settings that must be defined for using either the data Source or Sink:

- `ConnectionString` or `UseRbacAuth` - These define the authentication method used to connect to the Table API. Se further description and examples [here](#authentication-methods). One of these settings is required.
- `Table` - This defines the name of the Table to connect to on the Table API service. Such as the name of the Azure Storage Table. This is required.

There are also a couple optional settings that can be configured on the AzureTableAPI to help with mapping data between the Source and Sink:

- `RowKeyFieldName` - This is used to specify a different field name when mapping data to / from the `RowKey` field of Azure Table API. Optional.
- `PartitionKeyFieldName` - This is used to specify a different field name when mapping data to / from the `PartitionKey` field of Azure Table API. Optional.

In the Azure Table API, the `RowKey` and `PartitionKey` are required fields on the entities storage in a Table. When performing mapping of data between Azure Table API and Cosmos DB (or some other data store), you may be required to use a different field name in the other data store than these names as required by the Azure Table API. The `RowKeyFieldName` and `PartitionKeyFieldName` enables these fields to be mapped to / from a custom field name that matches your data requirements. If these settings are not specified, then these fields will not be renamed in the data mapping and will remain as they are in the Azure Table API.

### Additional Source Settings

The AzureTableAPI Source extension has an additional setting that can be configured for helping with querying data.

The following setting is supported for the Source:

- `QueryFilter` - This enables you to specify an OData filter to be applied to the data being retrieved by the AzureTableAPI Source. This is used in cases where only a subset of data from the source Table is needed in the migration. Example usage to query a subset of entities from the source table: `PartitionKey eq 'foo'`.

#### Query Filter Examples

The `QueryFilter` setting supports OData filter syntax for querying Azure Table API entities. Below are examples of common filter patterns:

**Basic Filters:**
```json
"QueryFilter": "PartitionKey eq 'WI'"
```

**DateTime Filters:**

When filtering by `Timestamp` or other datetime properties, you must use the `datetime` prefix with ISO 8601 format timestamps. In JSON configuration files, single quotes around the datetime value must be JSON-escaped as `\u0027`:

```json
"QueryFilter": "Timestamp eq datetime\u00272023-01-12T16:53:31.1714422Z\u0027"
```

```json
"QueryFilter": "Timestamp ge datetime\u00272023-05-15T03:30:32.663Z\u0027"
```

```json
"QueryFilter": "Timestamp lt datetime\u00272024-12-08T06:06:00.976Z\u0027"
```

**DateTime Range Filters:**

To filter entities within a date range, combine multiple conditions with `and`:

```json
"QueryFilter": "Timestamp ge datetime\u00272023-01-01T00:00:00Z\u0027 and Timestamp lt datetime\u00272024-01-01T00:00:00Z\u0027"
```

**Combined Filters:**

You can combine partition key filters with datetime filters for more efficient queries:

```json
"QueryFilter": "PartitionKey eq \u0027users\u0027 and Timestamp ge datetime\u00272023-05-15T00:00:00Z\u0027"
```

> **Important Notes:**
> - DateTime values must be in ISO 8601 format: `YYYY-MM-DDTHH:mm:ss.fffZ`
> - The `datetime` prefix is required before the timestamp value
> - Single quotes around datetime values must be JSON-escaped as `\u0027` in JSON configuration files
> - The `Z` suffix indicates UTC time
> - For better query performance, include `PartitionKey` in your filter when possible
> - Supported datetime operators: `eq` (equal), `ne` (not equal), `gt` (greater than), `ge` (greater than or equal), `lt` (less than), `le` (less than or equal)

### Additional Sink Settings

The AzureTableAPI Sink extension has additional settings that can be configured for writing Table entities.

The following settings are supported for the Sink:

- `MaxConcurrentEntityWrites` - The Maximum number of concurrent entity writes to the Azure Table API. This setting is used to control the number of concurrent writes to the Azure Table API.
- `WriteMode` - Specifies the behavior when writing entities to the table. Options are:
  - `Create` (default): Creates new entities only. Fails if an entity with the same partition key and row key already exists.
  - `Replace`: Upserts entities, completely replacing existing ones if they exist.
  - `Merge`: Upserts entities, merging properties with existing entities if they exist.

## Authentication Methods

The AzureTableAPI extension supports two authentication methods for connecting to Azure Table API services:

- **Connection String Authentication**: Use the `ConnectionString` property to specify the account connection string, which includes the account name and key.
- **Azure RBAC (Role Based Access Control) Authentication**: Set `UseRbacAuth` to `true` to use Entra credentials for authentication. When using RBAC, you must also specify the `AccountEndpoint` property (the Table service endpoint URL). Optionally, set `EnableInteractiveCredentials` to `true` to prompt for login if default credentials are not available (for example, when running locally).

> **Note**: To use RBAC authentication, your Azure account must have the appropriate permissions (such as Storage Table Data Contributor) on the storage account. For more information, see [Authorize access to tables using Microsoft Entra ID](https://learn.microsoft.com/en-us/azure/storage/tables/authorize-access-azure-active-directory).

### Example RBAC Settings

**Source Example (RBAC)**
```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://<storage-account-name>.table.core.windows.net",
  "Table": "SourceTable1",
  "PartitionKeyFieldName": "State",
  "RowKeyFieldName": "id",
  "EnableInteractiveCredentials": true,
  "QueryFilter": "PartitionKey eq 'WI'"
}
```

**Sink Example (RBAC)**
```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://<storage-account-name>.table.core.windows.net",
  "Table": "DestinationTable1",
  "PartitionKeyFieldName": "State",
  "RowKeyFieldName": "id",
  "EnableInteractiveCredentials": true,
  "WriteMode": "Merge",
  "MaxConcurrentEntityWrites": 10
}
```

### Example ConnectionString Source and Sink Settings Usage

The following are a couple example `settings.json` files for configuring the AzureTableAPI Source and Sink extensions.

**AzureTableAPISourceSettings.json**

```json
{
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<storage-account-name>;AccountKey=<key>;EndpointSuffix=core.windows.net",
  "Table": "SourceTable1",
  "PartitionKeyFieldName": "State",
  "RowKeyFieldName": "id",
  "QueryFilter": "PartitionKey eq 'WI'"
}
```

**AzureTableAPISinkSettings.json**

```json
{
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<storage-account-name>;AccountKey=<key>;EndpointSuffix=core.windows.net",
  "Table": "DestinationTable1",
  "PartitionKeyFieldName": "State",
  "RowKeyFieldName": "id",
  "WriteMode": "Replace",
  "MaxConcurrentEntityWrites": 5
}
```

### Example DateTime Filter Configurations

The following examples demonstrate how to use datetime filters in the `QueryFilter` setting:

**Example 1: Filter entities modified after a specific date**

```json
{
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<storage-account-name>;AccountKey=<key>;EndpointSuffix=core.windows.net",
  "Table": "SourceTable1",
  "PartitionKeyFieldName": "PartitionKey",
  "RowKeyFieldName": "RowKey",
  "QueryFilter": "Timestamp ge datetime\u00272023-05-15T03:30:32.663Z\u0027"
}
```

**Example 2: Filter entities within a date range**

```json
{
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<storage-account-name>;AccountKey=<key>;EndpointSuffix=core.windows.net",
  "Table": "SourceTable1",
  "QueryFilter": "Timestamp ge datetime\u00272023-01-01T00:00:00Z\u0027 and Timestamp lt datetime\u00272024-01-01T00:00:00Z\u0027"
}
```

**Example 3: Combine partition key with datetime filter**

```json
{
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<storage-account-name>;AccountKey=<key>;EndpointSuffix=core.windows.net",
  "Table": "SourceTable1",
  "PartitionKeyFieldName": "State",
  "RowKeyFieldName": "id",
  "QueryFilter": "PartitionKey eq \u0027CA\u0027 and Timestamp ge datetime\u00272023-06-01T00:00:00Z\u0027"
}
```
