using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.PostgresqlExtension.Settings
{

    public class PostgreSinkSettings : PostgreBaseSettings
    {
        [Required]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public string TableName { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public bool? AppendDataToTable { get; set; }
        public bool? DropAndCreateTable { get; set; }

    }
}