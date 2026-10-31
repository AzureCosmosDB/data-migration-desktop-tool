# Import the bundled JSON sample into SQL Server

This walkthrough imports [sample-data.zip](../sample-data.zip) into
`dbo.DmtSnapshot` using the JSON source and SQL Server sink. The archive contains
286 files under `sample-data/`, each containing one JSON object with 80 fields.
Use one file for a quick check or the whole extracted directory for a larger,
nullable dataset with nested properties.

The [table schema](dbo.DmtSnapshot.sql) and
[migration settings](migrationsettings.json) adapt the attachments in
[issue #204](https://github.com/AzureCosmosDB/data-migration-desktop-tool/issues/204).
They do not create a database or change the importer's behavior.

## Prerequisites

- Download and fully extract the [DMT release](https://github.com/AzureCosmosDB/data-migration-desktop-tool/releases)
  for your platform, keeping its `Extensions` directory. The `JSON` source and
  `SqlServer` sink must be available (`dmt list`).
- For a .NET 10-based DMT build, use a supported platform and the complete matching
  extension set; see [runtime support](../../docs/runtime-support.md).
  Installing a newer host SDK does not update an older self-contained DMT
  download. If building from source instead, follow the
  [pinned SDK instructions](../../README.md#getting-started).
- Use an existing SQL Server instance and an **explicitly selected disposable/test
  database**. You need permission to create the table and insert/select its rows.
  Never use a production database for this walkthrough.
- Use a SQL query client, such as SQL Server Management Studio or the MSSQL
  extension for Visual Studio Code. The verification queries using `ISJSON` and
  `JSON_VALUE` require SQL Server 2016 or later; see
  [JSON support in SQL Server](https://learn.microsoft.com/sql/relational-databases/json/json-data-sql-server).
- Download `sample-data.zip`, `dbo.DmtSnapshot.sql`, and `migrationsettings.json`
  from the links above into a new working directory, separate from the DMT
  installation. Use the **raw/download** buttons, not the GitHub HTML pages.

The commands below run from that working directory, not from the DMT installation
or the repository root. Replace executable paths and connection placeholders with
your own test environment values. No build or IDE is required.

## Extract the source files

DMT does **not** extract ZIP archives. Extract first, retaining the archive's
`sample-data` directory:

PowerShell (Windows):

```powershell
Set-Location 'C:\dmt-sql-sample'
Expand-Archive -LiteralPath .\sample-data.zip -DestinationPath .
(Get-ChildItem .\sample-data -Filter *.json -Recurse -File).Count
```

Bash (macOS/Linux, with `unzip` installed):

```bash
cd "$HOME/dmt-sql-sample"
unzip sample-data.zip
find sample-data -type f -name '*.json' | wc -l
```

The count should be **286**. The working directory should now contain:

```text
sample-data.zip
dbo.DmtSnapshot.sql
migrationsettings.json
sample-data/
    data_20090110.json
    ...
    data_20220505.json
    ...
    data_20220920.json
```

Keep unrelated JSON files out of `sample-data`; a directory source is recursive.

## Create the target table

In your SQL query client, connect to your test server and explicitly select your
test database (for example, `DmtSample`). Confirm the connection context:

```sql
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
```

Open and execute [dbo.DmtSnapshot.sql](dbo.DmtSnapshot.sql) in that database.
The SQL sink requires the table to exist before importing. The script deliberately
fails if `dbo.DmtSnapshot` already exists; it does not drop or truncate anything.
For a clean run, use a new disposable database or choose another unused table
name in **both** the schema and `SinkSettings.TableName`.

The table has 81 columns: 80 mapped source fields plus an unmapped
`Id INT IDENTITY(1,1)` primary key. SQL Server generates `Id`; do not add it to
`ColumnMappings` or turn on `IDENTITY_INSERT`.

## Set the connection

The settings file intentionally leaves `SinkSettings.ConnectionString` empty.
Supply it at runtime with the `SinkSettings__ConnectionString` environment variable
(two underscores). Environment settings override the settings file. Avoid putting
passwords in tracked files or command-line arguments.

For Windows authentication to a SQL Server configured with a trusted TLS
certificate, replace `YOUR_TEST_SERVER` and `DmtSample`:

```powershell
$dmt = 'C:\tools\dmt\dmt.exe'
$env:SinkSettings__ConnectionString = 'Server=YOUR_TEST_SERVER;Database=DmtSample;Integrated Security=True;Encrypt=True;TrustServerCertificate=False;'
```

On macOS/Linux, set the executable and use an appropriate connection string for
your test server. This Bash prompt hides input and keeps the value out of shell
history; for SQL authentication it would include `User ID` and `Password`.

```bash
dmt="$HOME/tools/dmt/dmt"
read -r -s -p 'Test database connection string: ' SinkSettings__ConnectionString
printf '\n'
export SinkSettings__ConnectionString
```

Include `Server`, the explicit test `Database`, the authentication settings, and
`Encrypt=True;TrustServerCertificate=False;`. The connection must use an identity
authorized in that database. If a disposable local server uses a self-signed
certificate, `TrustServerCertificate=True` bypasses certificate validation; use
that only when you knowingly trust that local test server, not as a general
connection fix.

## Import one file or the full folder

Choose one of the following runs. `--settings` selects the checked-in configuration;
`--SourceSettings:FilePath` overrides just the source path. Relative paths resolve
from the **current working directory**, not the settings file's directory.

For the single file `data_20220505.json`, the default in the settings file:

```powershell
& $dmt run --settings .\migrationsettings.json
```

```bash
"$dmt" run --settings ./migrationsettings.json
```

For every JSON file in the extracted folder:

```powershell
& $dmt run --settings .\migrationsettings.json --SourceSettings:FilePath .\sample-data
```

```bash
"$dmt" run --settings ./migrationsettings.json --SourceSettings:FilePath ./sample-data
```

Check the process exit status (`$LASTEXITCODE` in PowerShell or `$?` in Bash) and
logs, then run the SQL queries below. If the count is zero, confirm the source path
and database selection; a nonexistent file path can produce no input records.

**Reruns append rows.** This sample explicitly uses `WriteMode: Insert`.
An identity primary key does not deduplicate snapshots: running the single file
and then the full folder adds 287 rows, including two copies of the single-file
snapshot. Running the full folder twice adds 572 rows. No cleanup or upsert is
performed. Use a fresh table for the expected counts below, or compare against
the count recorded before your run.

## Verify the import

Run these queries in the same database used in the connection string:

```sql
SELECT COUNT(*) AS TotalRows,
       COUNT(DISTINCT Id) AS DistinctIds,
       SUM(CASE WHEN ISJSON(Properties) = 1 THEN 1 ELSE 0 END) AS ValidPropertiesJson
FROM dbo.DmtSnapshot;

SELECT Granularity, COUNT(*) AS RowsPerGranularity
FROM dbo.DmtSnapshot
GROUP BY Granularity
ORDER BY Granularity;

SELECT MIN([Date]) AS EarliestDate, MAX([Date]) AS LatestDate
FROM dbo.DmtSnapshot;

SELECT Id, Ticker, [Date], Granularity, ActiveAddresses,
       JSON_VALUE(Properties, '$.TransactionRate') AS TransactionRate
FROM dbo.DmtSnapshot
WHERE ActiveAddresses = N'1174988.000'
  AND JSON_VALUE(Properties, '$.TransactionRate') = N'3.653';
```

Expected results for an initially empty table:

| Run | TotalRows / DistinctIds / ValidPropertiesJson | RowsPerGranularity |
| --- | --- | --- |
| Single file | 1 / 1 / 1 | 60: 1 |
| Full folder | 286 / 286 / 286 | 40: 1, 60: 285 |
| Single file, then full folder | 287 / 287 / 287 | 40: 1, 60: 286 |

The last query returns the May 5 snapshot: `Ticker = BTC`, `Granularity = 60`,
`ActiveAddresses = 1174988.000`, and `TransactionRate = 3.653`. Expect one row for
either fresh-table run, or two if you ran the single file and then the folder.
Identity values are not source identifiers and should not be used to identify a
particular snapshot.

### Dates, nulls, and nested properties

`Date` is mapped as `System.DateTime` to SQL `DATETIME`; it is **not** a time-zone
preserving mapping. The existing importer converts timestamp strings ending in
`Z` to the importing machine's local time before storing the wall-clock value.
On a UTC machine the single-file value is `2022-05-05 23:59:00`; for example, on a
Pacific-time machine it is `2022-05-05 16:59:00`. For a full-folder run on a UTC
machine the date range is `2009-01-10 23:59:00` through `2022-09-20 21:01:58`.
The latest source timestamp has no offset. Do not assume the stored values are
all UTC or compare date-only literals without accounting for the importing
machine's time zone.

All 80 mapped SQL columns are nullable; `AllowNull` defaults to `true`. The null
string fields in the bundled data remain SQL `NULL` (there are no SQL defaults).
For example, `Name` is null in every record; `Date` and `Granularity` are populated
in every record. Metrics are mostly numeric-looking
**strings**, intentionally mapped to `NVARCHAR(50)` rather than numeric types.
The longest metric string in this archive is 28 characters. Convert explicitly
in queries if you need numeric calculations.

`Properties` is a nested object serialized to JSON text in `NVARCHAR(MAX)`, not
flattened into relational columns. Its keys vary by record; `JSON_VALUE` returns
`NULL` for missing keys. `MemPoolStateByFeeLevel` also uses `NVARCHAR(MAX)`.
Field spellings such as `AdjusedSupply`, `Thremocap`, and the nested
`VolumneMean` are intentional matches to the source; correcting their spelling
would break name-based lookups.

When finished, remove the connection setting from this shell:

```powershell
Remove-Item Env:\SinkSettings__ConnectionString
```

```bash
unset SinkSettings__ConnectionString
```

See the [SQL Server extension settings](../../Extensions/SqlServer/README.md) for
other datasets and write modes.
