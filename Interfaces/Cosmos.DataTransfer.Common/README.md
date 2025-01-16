# File Storage Extension

The File Storage extension provides reading and writing of formatted files on local disk or reading from publicly accessible URLs.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Source and sink settings both require a `FilePath` parameter, which should specify a path to a formatted file or folder containing multiple json-files. The path can be either absolute or relative to the application. 

### Source

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

```json
{
    "FilePath": "",
    "Compression": "None" | "Gzip" | "Brotli" | "Deflate"
}
```
