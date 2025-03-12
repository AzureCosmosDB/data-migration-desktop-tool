# SQL Server Extension

The SQL Server data transfer extension provides source and sink capabilities for reading from and writing to table data in SQL Server. Hierarchical data is not fully supported but the Sink extension will write a nested object from a hierarchical source (i.e. JSON, Cosmos) into a SQL column as a JSON string.

> **Note**: When specifying the SQL Server extension as the Source or Sink property in configuration, utilize the name **SqlServer**.

## Settings

Source and sink settings both require a `ConnectionString` parameter.

If surfacing data as JSON, the `JsonFields` parameter can be used to specify which fields should be treated as JSON strings. This is useful for building nested sets of data that should be handled as a nested object downstream.

### Source

Source settings also require either a `QueryText` or `FilePath` parameter with the SQL statement that defines the data to select.
`QueryText` can be the SQL statement, while `FilePath` can point to a file with the SQL statement.
In both cases, the SQL statement can combine data from multiple tables, views, etc.,
but should produce a single result set.

```json
{
    "ConnectionString": "",
    "QueryText": "", // required if FilePath not set.
    "FilePath": ""   // required if QueryText not set.
}
```

### Example Source settings

The extension supports parameterized queries, using named placeholders.
Use `@param-name` for the named parameters.
Positional parameters are *not* supported.

Example (works better when query is in a separate file though):

```json
{
    "ConnectionString": "",
    "QueryText": "SELECT * FROM Logs WHERE UserId = @id",
    "Parameters": {
        "@id": "johndoe"
    }
}
```

This example illustrates the usage of JsonFields. You can see the subquery aliased as 'OrderLineItems' from Sales.OrderLines that is returned as Json using the 'FOR JSON AUTO' clause. This field is then referenced in the JsonFields Array.

```json
{
    "ConnectionString": "Server=.;Database=WideWorldImporters;Trusted_Connection=True;TrustServerCertificate=True;",
    "IncludeMetadataFields": false,
    "QueryText": "SELECT TOP 1000 o.OrderID , c.CustomerName , o.OrderDate , o.ExpectedDeliveryDate , o.CustomerPurchaseOrderNumber , (select li.Description, li.Quantity FROM Sales.OrderLines li WHERE li.OrderID= o.OrderID FOR JSON AUTO) OrderLineItems FROM Sales.Customers c JOIN Sales.Orders o ON o.CustomerID = c.CustomerID ORDER BY OrderId",
    "JsonFields": [
        "OrderLineItems"
    ]
}
```

Example Nested JSON Output

```json
{
    "OrderID": 2,
    "CustomerName": "Bala Dixit",
    "OrderDate": "2013-01-01T00:00:00.0000000",
    "ExpectedDeliveryDate": "2013-01-02T00:00:00.0000000",
    "CustomerPurchaseOrderNumber": "15342",
    "OrderLineItems": [
        {
        "Description": "Developer joke mug - old C developers never die (White)",
        "Quantity": 9
        },
        {
        "Description": "USB food flash drive - chocolate bar",
        "Quantity": 9
        }
    ]
}
```

### Sink

Sink settings require a `TableName` to define where to insert data and an array of `ColumnMappings`. Only fields listed in `ColumnMappings` will be imported. Each element in `ColumnMappings` requires a `ColumnName` specifying the target SQL column along with situation specific fields:

- `SourceFieldName`: This should be set in cases where the source data uses a different name than the SQL column. Column name to source field mapping defaults to using the `ColumnName` for both sides and is case-insensitive so it is not necessary to specify this parameter for mappings like `"id"` -> `"Id"`.
- `AllowNull`: Depending on the table schema you may need to force values to be set for columns when no value is present in the source. If this is set to `false` this column will use the `DefaultValue` for any records missing a source value. Defaults to `true`.
- `DefaultValue`: Value to be used in place of missing or null source fields. This parameter is ignored unless `AllowNull` is set to `false` for this column.
- `DataType`: This setting specify the DataType of the column, default will be String: please refer documentation. <https://learn.microsoft.com/sql/relational-databases/clr-integration-database-objects-types-net-framework/mapping-clr-parameter-data?view=sql-server-ver16&redirectedfrom=MSDN&tabs=csharp>

Sink settings also include an optional `BatchSize` parameter to specify the count of records to accumulate before bulk inserting, default value is 1000.

```json
{
    "ConnectionString": "",
    "TableName": "MyStagingTable",
    "ColumnMappings": [
        {
            "ColumnName": "Id"
        },
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
            "ColumnName": "IsSet",
            "AllowNull": false,
            "DefaultValue": false
            "DataType": "System.Boolean"
        }
    ],
    "BatchSize": 1000
}
```
