using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.PostgresqlExtension.Settings
{

    public class PostgreSinkSettings : PostgreBaseSettings
    {
        public string TableName { get; set; };
        public bool? CreateTableIfNotExist { get; set; }
        public bool? DropTabelIfExists { get; set; }

    }
}
