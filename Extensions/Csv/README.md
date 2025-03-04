# CSV Extension

The CSV extension provides formatter capabilities for reading from and writing to CSV files.

> **Note**: This is a File Format extension that is only used in combination with Binary Storage extensions. 

> **Note**: When specifying the CSV extension as the Source or Sink property in configuration, utilize the names listed below.

Supported storage sinks:
- File - **Csv**
- Azure Blob Storage - **Csv-AzureBlob**
- AWS S3 - **Csv-AwsS3**
 
Supported storage sources:
- File - **Csv**
- Azure Blob Storage - **Csv-AzureBlob**
- AWS S3 - **Csv-AwsS3**

## Settings

See storage extension documentation for any storage specific settings needed ([ex. File Storage](../../Interfaces/Cosmos.DataTransfer.Common/README.md)).

### Source

Source supports an optional `Delimiter` parameter (`,` by default) and an optional `HasHeader` parameter (`true` by default). For files without a header, column names will be generated based on the `ColumnNameFormat` setting, which uses a default value of `column_{0}` to produce columns `column_0`, `column_1`, etc.


```json
{
    "Delimiter": ",",
    "HasHeader": true
}
```

### Sink

Sink supports an optional `Delimiter` parameter (`,` by default) and an optional `IncludeHeader` parameter (`true` by default) to add a leading row of column names.

Formatting options, or locale, can be set with an optional `Culture` setting (`"InvariantCulture"` by default). 
This specifies how e.g., numbers and dates are formatted according to a specific culture. 
Set to `"InvariantCulture"` to use the system's or process' current locale setting 
(see [CultureInfo.CurrentCulture](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.currentculture)),
or e.g., `"en"`, `"en-GB"`, or `"en-US"` for English standards (period, `.`, as decimal separator and other regional standards), 
"da-DK" for Danish (comma, `,`, as decimal separator), etc.
Note, if using a culture with comma as decimal separator, specify a different delimiter (e.g., semi-colon, `;`), else all numbers
will be written enclosed with quotes.

```json
{
    "Delimiter": ",",
    "IncludeHeader": true,
    "Culture": "Invariant"
}
```
