# File Storage Extension

The File Storage extension provides reading and writing of formatted files on local disk or reading from publicly accessible URLs.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Source and sink settings both require a `FilePath` parameter, which should specify a path to a formatted file or folder containing multiple files of the same type. The path can be either absolute or relative to the application. 

### Source

```json
{
    "FilePath": ""
}

```

### Sink

```json
{
    "FilePath": ""
}
```
