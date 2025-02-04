using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.PostgresqlExtension.Settings
{

    public class PostgreSinkSettings : PostgreBaseSettings
    {
        [Required]
        public string TableName { get; set; }
        public bool? AppendDataToTable { get; set; }
        public bool? DropAndCreateTable { get; set; }

    }
}