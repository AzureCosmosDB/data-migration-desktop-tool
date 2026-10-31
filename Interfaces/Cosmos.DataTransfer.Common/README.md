# File Storage Extension

The File Storage extension provides reading and writing of formatted files on local disk or reading from publicly accessible URLs.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Source and sink settings both require a `FilePath` parameter, which should specify a path to a formatted file or folder containing multiple json-files. The path can be either absolute or relative to the application. 

### Source

Missing or unreadable source paths, HTTP connection failures, and non-success HTTP responses fail the migration (CLI exit code `1`); they are not treated as empty sources. An existing directory with no matching files still produces an empty source. HTTP diagnostics omit URL credentials, query strings, and fragments.

HTTP responses are streamed rather than buffered in full. Receiving response headers uses the HTTP client's default 100-second timeout. Each subsequent body read has a 100-second inactivity timeout and honors migration cancellation. A stalled read fails the migration; a transfer that continues returning data can run longer than 100 seconds. Time spent processing records between reads does not count toward the body-read timeout. This behavior applies to all file-format extensions using the HTTP source.

Gzip, bzip2, and zlib compressed files are automatically detected by file extension
(`.gz`, `.bz2`, and `.zz`, respectively) and decompressed, when `"None"` is specified as `"Compression"`.

```json
{
    "FilePath": "",
    "Compression": "None" | "Gzip" | "Brotli" | "Deflate"
}

```

### Sink

Use parameter `Compression` to enable compression of output file. 
The relevant extension is automatically added to the file name, if missing.

With `Append: false` (the default), local file sinks (JSON, CSV, and Parquet)
write to a uniquely named `.dmt-<id>.tmp` sibling in the destination directory.
The configured output is published only after the source has been consumed and
formatting, compression, and stream disposal have succeeded. Source/parse errors,
cancellation observed before publication, and write/finalization failures leave an
existing destination unchanged, or leave no destination if it did not exist.
These failures retain CLI exit code `1`. Compression uses the same policy for
Gzip, Brotli, and Deflate, including the automatically added file extension.

The destination directory must allow creating and replacing files, and have
space for the entire new output alongside any old destination. Existing files
are replaced, not truncated in place: Windows replacement preserves destination
metadata supported by `File.Replace`; on Unix, permission bits are copied to the
new file, but file identity, ownership, timestamps, and hard-link relationships
are not guaranteed to be preserved. Existing destination access permissions
(Windows DACL or Unix mode bits) are applied to staging before writing data.
Read-only destinations and symbolic-link/reparse-point destinations are rejected
with an error; links and their targets are left unchanged. To overwrite a link's
target, explicitly configure the regular target file path instead.
Ordinary same-path file source/destination migrations read the original before
replacement. Directory sources do not read `.tmp` staging files. Do not have
multiple processes modify the destination during a migration.

Temporary files are removed on handled failure. If cleanup fails, the error log
identifies the temporary path for manual removal without hiding the original
failure. A process kill or machine failure can leave `.dmt-*.tmp` files; these are
**not completed outputs** and can be removed after confirming the owning
migration is no longer running. Stale files are never automatically reused or
deleted by a later migration. Publication is a per-file filesystem operation,
not a multi-operation transaction or a power-loss durability guarantee.

With `Append: true`, output is written **directly** to the destination, retaining
the existing byte-append behavior. Failure or cancellation can leave partial
appended data (or a new partial file), and the error log explicitly warns that
no rollback occurred. Retrying can duplicate data. Append concatenates formatter
output; it does not merge JSON arrays or repair other structured file formats.
Append remains incompatible with all compression methods.

```json
{
    "FilePath": "",
    "Compression": "None" | "Gzip" | "Brotli" | "Deflate",
    "Append": false
}
```
