using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common;

public class FileSinkSettings : IDataExtensionSettings
{
    [Required]
    public string? FilePath { get; set; }
}