using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Core
{
    public class InitCommand : Command
    {
        public InitCommand()
            : base("init", "Creates template settings file for use as input")
        {
            AddInitOptions(this);
        }
        
        public static void AddInitOptions(Command command)
        {
            var settingsOption = new Option<FileInfo?>(
                aliases: new[] { "--path", "-p" },
                description: "The settings file to create. (default: migrationsettings.json)");
            var multiOption = new Option<bool?>(
                aliases: new[] { "--multi", "-m" },
                description: "True to include an Operations array for adding multiple data transfer operations in a single run");

            command.AddOption(settingsOption);
            command.AddOption(multiOption);
        }
        
        public class CommandHandler : ICommandHandler
        {
            private readonly ILogger<CommandHandler> _logger;

            public FileInfo? Path { get; set; }
            public bool? Multi { get; set; }

            public CommandHandler(ILogger<CommandHandler> logger)
            {
                _logger = logger;
            }

            public int Invoke(InvocationContext context)
            {
                return InvokeAsync(context).GetAwaiter().GetResult();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var options = new JsonSerializerOptions { WriteIndented = true, };
                string? json;
                if (Multi != true)
                {
                    json = JsonSerializer.Serialize(new
                    {
                        Source = (string?)null,
                        Sink = (string?)null,
                        SourceSettings = new { },
                        SinkSettings = new { },
                    }, options);
                }
                else
                {
                    json = JsonSerializer.Serialize(new
                    {
                        Source = (string?)null,
                        Sink = (string?)null,
                        SourceSettings = new { },
                        SinkSettings = new { },
                        Operations = new[]
                        {
                            new
                            {
                                SourceSettings = new { },
                                SinkSettings = new { },
                            },
                            new
                            {
                                SourceSettings = new { },
                                SinkSettings = new { },
                            }
                        }
                    }, options);
                }
                await File.WriteAllTextAsync(Path?.FullName ?? "migrationsettings.json", json, context.GetCancellationToken());
                return 0;
            }
        }
    }
}