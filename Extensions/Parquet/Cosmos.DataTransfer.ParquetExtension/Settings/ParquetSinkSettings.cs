using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.ParquetExtension.Settings
{
    public class ParquetSinkSettings : IDataExtensionSettings
    {
        // Add option to set a custom row group size for very large files.
        //public int? CustomRowGroupSize { get; set; }
    }
}