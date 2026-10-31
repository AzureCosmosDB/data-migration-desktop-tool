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

It is possible to build further upon a file by setting the `Append` parameter
to `true`. Note however that it is not compatible with any compression methods,
as the result a concatenation of multiple compressed files.

```json
{
    "FilePath": "",
    "Compression": "None" | "Gzip" | "Brotli" | "Deflate",
    "Append": false
}
```
