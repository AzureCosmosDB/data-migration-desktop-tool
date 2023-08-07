using System.Text.Json;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.Ui.Common
{
    public class ExtensionSetting
    {
        public ExtensionSetting(ExtensionSettingProperty definition)
        {
            Definition = definition;
            if (Definition.DefaultValue != null)
            {
                if (Definition.DefaultValue is JsonElement element)
                {
                    try
                    {
                        switch (Definition.Type)
                        {
                            case PropertyType.String:
                            case PropertyType.Enum:
                                Value = element.GetString();
                                break;
                            case PropertyType.Boolean:
                                Value = element.GetBoolean();
                                break;
                            case PropertyType.Int:
                                Value = element.GetInt32();
                                break;
                            case PropertyType.Float:
                                Value = element.GetDouble();
                                break;
                            case PropertyType.DateTime:
                                Value = element.GetDateTime();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        
                    }
                }
                else
                {
                    Value = Definition.DefaultValue;
                }
            }
        }

        public ExtensionSettingProperty Definition { get; init; }

        public object? Value { get; set; }

        public string? StringValue
        {
            get => Value as string;
            set => Value = value;
        }

        public DateTime? DateValue
        {
            get => Value as DateTime?;
            set => Value = value;
        }
        public int? IntValue
        {
            get => Value as int?;
            set => Value = value;
        }
        public double? FloatValue
        {
            get => Value as double?;
            set => Value = value;
        }
        public bool? BooleanValue
        {
            get => Value as bool?;
            set => Value = value;
        }

        public static string ExampleUndeclared = "\"ConnectionString\": \"Server=server\",\n\"Database\": \"myDb\"";
    }
}

