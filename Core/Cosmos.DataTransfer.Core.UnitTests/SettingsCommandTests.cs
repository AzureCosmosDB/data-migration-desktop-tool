using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cosmos.DataTransfer.Core.UnitTests
{
    [TestClass]
    public class SettingsCommandTests
    {
        [TestMethod]
        public void Invoke_ForTestExtension_ProducesValidSettingsJson()
        {
            var command = new SettingsCommand();
            const string source = "testSource";
            var loader = new Mock<IExtensionManifestBuilder>();
            var sourceExtension = new Mock<IDataSourceExtensionWithSettings>();
            sourceExtension.SetupGet(ds => ds.DisplayName).Returns(source);
            sourceExtension.Setup(ds => ds.GetSettings()).Returns(new List<IDataExtensionSettings>
            {
                new MockExtensionSettings(),
                new MockExtensionSettings2()
            });
            loader
                .Setup(l => l.GetSources())
                .Returns(new List<IDataSourceExtension> { sourceExtension.Object });

            var writer = new Mock<IRawOutputWriter>();
            var outputLines = new List<string>();
            writer.Setup(w => w.WriteLine(It.IsAny<string>())).Callback<string>(s => outputLines.Add(s));
            var handler = new SettingsCommand.CommandHandler(loader.Object, writer.Object, NullLogger<SettingsCommand.CommandHandler>.Instance)
            {
                Source = true,
                Extension = source
            };

            var parseResult = new SettingsCommand().Parse(Array.Empty<string>());
            var result = handler.Invoke(new InvocationContext(parseResult));
            Assert.AreEqual(0, result);

            bool jsonStarted = false;
            var stringBuilder = new StringBuilder();
            foreach (string item in outputLines)
            {
                if (item == "<<<")
                    jsonStarted = true;
                else if (item == ">>>")
                    jsonStarted = false;
                else if (jsonStarted)
                    stringBuilder.AppendLine(item);
            }

            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                WriteIndented = true
            };
            var fullJson = stringBuilder.ToString().Trim();
            var parsed = JsonSerializer.Deserialize<List<ExtensionSettingProperty>>(fullJson, options);
            var parsedJson = JsonSerializer.Serialize<List<ExtensionSettingProperty>>(parsed, options);

            Assert.AreEqual(fullJson, parsedJson);
        }
    }

    public class MockExtensionSettings : IDataExtensionSettings
    {
        [Required]
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int Count { get; set; } = 99;
        public int? MaxValue { get; set; }
        public double? Avg { get; set; }
        public bool Enabled { get; set; } = true;
        public JsonCommentHandling TestEnum { get; set; }
    }

    public class MockExtensionSettings2 : IDataExtensionSettings
    {
        [Required]
        public string? AnotherProperty { get; set; }
    }
}