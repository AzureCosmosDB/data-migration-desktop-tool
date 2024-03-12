# PostgreSQL Extension

The PostgreSQL data transfer extension provides source and sink capabilities for reading from and writing to table data in PostgreSQL Server.

> **Note**: When specifying the PostgreSQL extension as the Source or Sink property in configuration, utilize the name **PostgreSQL**.

## Settings

Source and sink settings both require a `ConnectionString` parameter. Specify the database name in the connection string.

Source settings also require a `QueryText` parameter to define the data to select from SQL. This can combine data from multiple tables, views, etc. but should produce a single result set.

### Source

```json
{
    "ConnectionString": "",
    "QueryText": ""
}
```

Sink settings require a `TableName` to define where to insert data.
- `AppendDataToTable`: Set to true to use table's schema and append data to the table.
- `DropAndCreateTable`: Set to true to drop and recreate the table. Schema will be guessed from the source data.
 
### Sink

```json
{
    "ConnectionString": "",
    "TableName": "",
    "AppendDataToTable": true | false,
    "DropAndCreateTable": true | false
}
```