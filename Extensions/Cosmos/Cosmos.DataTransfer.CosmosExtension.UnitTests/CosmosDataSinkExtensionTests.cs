using Cosmos.DataTransfer.Interfaces;
using System.Dynamic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests
{
    [TestClass]
    public class CosmosDataSinkExtensionTests
    {
        [TestMethod]
        public void CreateItemStream_WithDateString_PreservesFormat()
        {
            // Arrange - Simulate data read from source with ISO-8601 date string
            var sourceSettings = RawJsonCosmosSerializer.GetDefaultSettings();
            var sourceJson = "{\"id\": \"1\", \"event_time\": \"2023-12-19T00:00:00.000Z\"}";
            var serializer = JsonSerializer.Create(sourceSettings);
            using var reader = new JsonTextReader(new StringReader(sourceJson));
            var sourceDict = serializer.Deserialize<Dictionary<string, object?>>(reader)!;
            
            // Act - Create data item and build expando object (simulating the pipeline)
            var dataItem = new CosmosDictionaryDataItem(sourceDict);
            var expando = dataItem.BuildDynamicObjectTree()!;
            
            // Serialize using the same settings that CreateItemStream uses
            var json = JsonConvert.SerializeObject(expando, RawJsonCosmosSerializer.GetDefaultSettings());
            
            // Assert - The date string format should be preserved
            Assert.IsTrue(json.Contains("\"2023-12-19T00:00:00.000Z\""), 
                $"Date format should be preserved. Actual JSON: {json}");
        }

        [TestMethod]
        public void CreateItemStream_WithNestedDateString_PreservesFormat()
        {
            // Arrange - Simulate data with nested date strings
            var sourceSettings = RawJsonCosmosSerializer.GetDefaultSettings();
            var sourceJson = "{\"id\": \"1\", \"data\": {\"created\": \"2023-12-19T00:00:00.000Z\", \"modified\": \"2023-12-20T12:30:45.123Z\"}}";
            var serializer = JsonSerializer.Create(sourceSettings);
            using var reader = new JsonTextReader(new StringReader(sourceJson));
            var sourceDict = serializer.Deserialize<Dictionary<string, object?>>(reader)!;
            
            // Act
            var dataItem = new CosmosDictionaryDataItem(sourceDict);
            var expando = dataItem.BuildDynamicObjectTree()!;
            var json = JsonConvert.SerializeObject(expando, RawJsonCosmosSerializer.GetDefaultSettings());
            
            // Assert
            Assert.IsTrue(json.Contains("\"2023-12-19T00:00:00.000Z\""), 
                $"Created date format should be preserved. Actual JSON: {json}");
            Assert.IsTrue(json.Contains("\"2023-12-20T12:30:45.123Z\""), 
                $"Modified date format should be preserved. Actual JSON: {json}");
        }

        [TestMethod]
        public void CreateItemStream_WithDateStringArray_PreservesFormat()
        {
            // Arrange - Simulate data with date strings in array
            var sourceSettings = RawJsonCosmosSerializer.GetDefaultSettings();
            var sourceJson = "{\"id\": \"1\", \"timestamps\": [\"2023-12-19T00:00:00.000Z\", \"2023-12-20T00:00:00.000Z\"]}";
            var serializer = JsonSerializer.Create(sourceSettings);
            using var reader = new JsonTextReader(new StringReader(sourceJson));
            var sourceDict = serializer.Deserialize<Dictionary<string, object?>>(reader)!;
            
            // Act
            var dataItem = new CosmosDictionaryDataItem(sourceDict);
            var expando = dataItem.BuildDynamicObjectTree()!;
            var json = JsonConvert.SerializeObject(expando, RawJsonCosmosSerializer.GetDefaultSettings());
            
            // Assert
            Assert.IsTrue(json.Contains("\"2023-12-19T00:00:00.000Z\""), 
                $"First date format should be preserved. Actual JSON: {json}");
            Assert.IsTrue(json.Contains("\"2023-12-20T00:00:00.000Z\""), 
                $"Second date format should be preserved. Actual JSON: {json}");
        }

        [TestMethod]
        public void BuildDynamicObjectTree_WithNestedArrays_WorksCorrectly()
        {
            var item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                {
                    "array",
                    new List<object?>
                    {
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-1" }
                            }),
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-2" }
                            })
                        },
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub2-1" }
                            }),
                        }
                    }
                }
            });

            dynamic obj = item.BuildDynamicObjectTree()!;

            Assert.AreEqual(typeof(object[]), obj.array.GetType());
            Assert.AreEqual(2, obj.array.Length);

            var firstSubArray = obj.array[0];
            Assert.AreEqual(typeof(object[]), firstSubArray.GetType());
            Assert.AreEqual(2, firstSubArray.Length);

            Assert.AreEqual("sub1-1", firstSubArray[0].id);
            Assert.AreEqual("sub1-2", firstSubArray[1].id);

            var secondSubArray = obj.array[1];
            Assert.AreEqual(typeof(object[]), secondSubArray.GetType());
            Assert.AreEqual(1, secondSubArray.Length);

            Assert.AreEqual("sub2-1", secondSubArray[0].id);
        }

        [TestMethod]
        public void BuildDynamicObjectTree_WithAnyCaseIds_UsesSourceIdValue()
        {
            var numeric = Random.Shared.Next();
            var lower = Guid.NewGuid().ToString();
            var upper = Guid.NewGuid().ToString();
            var mixed = Guid.NewGuid().ToString();
            var reversed = Guid.NewGuid().ToString();
            var item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "id", numeric },
            });

            dynamic obj = item.BuildDynamicObjectTree(requireStringId: true, preserveMixedCaseIds: false)!;
            Assert.AreEqual(numeric.ToString(), obj.id);

            item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "id", lower },
            });

            obj = item.BuildDynamicObjectTree(requireStringId: true, preserveMixedCaseIds: false)!;
            Assert.AreEqual(lower, obj.id);

            item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "ID", upper },
            });
            obj = item.BuildDynamicObjectTree(requireStringId: true, preserveMixedCaseIds: false)!;
            Assert.AreEqual(upper, obj.id);

            item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "Id", mixed },
            });
            obj = item.BuildDynamicObjectTree(requireStringId: true, preserveMixedCaseIds: false)!;
            Assert.AreEqual(mixed, obj.id);

            item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "iD", reversed },
            });
            obj = item.BuildDynamicObjectTree(requireStringId: true, preserveMixedCaseIds: false)!;
            Assert.AreEqual(reversed, obj.id);
        }

        [TestMethod]
        public void BuildDynamicObjectTree_WithPreservedMixedCaseIds_PassesThroughSourceValues()
        {
            var id = Random.Shared.Next();
            var upper = Guid.NewGuid().ToString();
            var mixed = Guid.NewGuid().ToString();
            var item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "id", id },
                { "ID", upper },
                { "Id", mixed }
            });

            dynamic obj = item.BuildDynamicObjectTree(requireStringId: true, preserveMixedCaseIds: true)!;
            Assert.AreEqual(id.ToString(), obj.id);
            Assert.AreEqual(upper, obj.ID);
            Assert.AreEqual(mixed, obj.Id);

            item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "ID", upper },
                { "Id", mixed }
            });
            obj = item.BuildDynamicObjectTree(requireStringId: true, preserveMixedCaseIds: true)!;
            Assert.AreEqual(upper, obj.ID);
            Assert.AreEqual(mixed, obj.Id);
            string? cosmosId = obj.id;
            Assert.IsNotNull(cosmosId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(cosmosId));
        }

        [TestMethod]
        public void BuildDynamicObjectTree_WithIgnoredNulls_ExcludesNullFields()
        {
            var item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "id", "1" },
                { "nullField", null },
                {
                    "array",
                    new List<object?>
                    {
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-1" },
                                { "nullField", null },
                            }),
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-2" }
                            })
                        },
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub2-1" },
                                { "nullField", null },
                            }),
                        }
                    }
                },
                { "child1",
                    new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                    {
                        { "id", "child1-1" },
                    })
                },
                { "child2",
                    new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                    {
                        { "id", "child2-1" },
                        { "nullField", null },
                        { "child2_1",
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "child2_1-1" },
                                { "nullField", null },
                            })
                        }
                    })
                }
            });

            dynamic obj = item.BuildDynamicObjectTree(ignoreNullValues: true)!;

            Assert.IsFalse(HasProperty(obj, "nullField"));

            Assert.AreEqual(typeof(object[]), obj.array.GetType());
            Assert.AreEqual(2, obj.array.Length);

            var firstSubArray = obj.array[0];
            Assert.AreEqual(typeof(object[]), firstSubArray.GetType());
            Assert.IsFalse(HasProperty(firstSubArray[0], "nullField"));

            var secondSubArray = obj.array[1];
            Assert.AreEqual(typeof(object[]), secondSubArray.GetType());
            Assert.IsFalse(HasProperty(secondSubArray[0], "nullField"));

            var child2 = obj.child2;
            Assert.IsFalse(HasProperty(child2, "nullField"));
            Assert.IsFalse(HasProperty(child2.child2_1, "nullField"));
        }

        [TestMethod]
        public void BuildDynamicObjectTree_WithNulls_RetainsNullFields()
        {
            var item = new CosmosDictionaryDataItem(new Dictionary<string, object?>()
            {
                { "id", "1" },
                { "nullField", null },
                {
                    "array",
                    new List<object?>
                    {
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-1" },
                                { "nullField", null },
                            }),
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub1-2" }
                            })
                        },
                        new List<object?>
                        {
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "sub2-1" },
                                { "nullField", null },
                            }),
                        }
                    }
                },
                { "child1",
                    new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                    {
                        { "id", "child1-1" },
                    })
                },
                { "child2",
                    new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                    {
                        { "id", "child2-1" },
                        { "nullField", null },
                        { "child2_1",
                            new CosmosDictionaryDataItem(new Dictionary<string, object?>()
                            {
                                { "id", "child2_1-1" },
                                { "nullField", null },
                            })
                        }
                    })
                }
            });

            dynamic obj = item.BuildDynamicObjectTree(ignoreNullValues: false)!;

            Assert.IsTrue(HasProperty(obj, "nullField"));
            Assert.IsNull(obj.nullField);

            Assert.AreEqual(typeof(object[]), obj.array.GetType());
            Assert.AreEqual(2, obj.array.Length);

            var firstSubArray = obj.array[0];
            Assert.AreEqual(typeof(object[]), firstSubArray.GetType());
            Assert.IsTrue(HasProperty(firstSubArray[0],"nullField"));
            Assert.IsNull(firstSubArray[0].nullField);
            Assert.IsFalse(HasProperty(firstSubArray[1], "nullField"));

            var secondSubArray = obj.array[1];
            Assert.AreEqual(typeof(object[]), secondSubArray.GetType());
            Assert.IsTrue(HasProperty(secondSubArray[0], "nullField"));
            Assert.IsNull(secondSubArray[0].nullField);

            var child2 = obj.child2;
            Assert.IsTrue(HasProperty(child2, "nullField"));
            Assert.IsNull(child2.nullField);
            Assert.IsTrue(HasProperty(child2.child2_1, "nullField"));
            Assert.IsNull(child2.child2_1.nullField);
        }

        public static bool HasProperty(object obj, string name)
        {
            if (obj is not ExpandoObject)
                return obj.GetType().GetProperty(name) != null;

            var values = (IDictionary<string, object>)obj;
            return values.ContainsKey(name);
        }

        private static string? InvokeGetPropertyValue(ExpandoObject item, string propertyName)
        {
            var method = typeof(CosmosDataSinkExtension).GetMethod("GetPropertyValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method?.Invoke(null, new object[] { item, propertyName }) as string;
        }

        [TestMethod]
        public void GetPropertyValue_WithSimpleProperty_ReturnsValue()
        {
            // Arrange
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            dict["id"] = "test-id-123";
            dict["name"] = "test-name";
            
            // Act
            var result = InvokeGetPropertyValue(expando, "id");
            
            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("test-id-123", result);
        }

        [TestMethod]
        public void GetPropertyValue_WithNestedProperty_ReturnsValue()
        {
            // Arrange - Create nested structure matching the issue example
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            dict["id"] = "test-id";
            
            var nestedExpando = new ExpandoObject();
            var nestedDict = (IDictionary<string, object?>)nestedExpando;
            nestedDict["partitionkeyvalue2"] = "guid-value-123";
            nestedDict["somevalue4"] = "other-guid";
            nestedDict["UserName"] = "testuser";
            
            dict["partitionkeyvalue1"] = nestedExpando;
            
            // Act
            var result = InvokeGetPropertyValue(expando, "partitionkeyvalue1/partitionkeyvalue2");
            
            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("guid-value-123", result);
        }

        [TestMethod]
        public void GetPropertyValue_WithDeeplyNestedProperty_ReturnsValue()
        {
            // Arrange - Create deeply nested structure
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            dict["id"] = "test-id";
            
            var level1 = new ExpandoObject();
            var level1Dict = (IDictionary<string, object?>)level1;
            
            var level2 = new ExpandoObject();
            var level2Dict = (IDictionary<string, object?>)level2;
            
            var level3 = new ExpandoObject();
            var level3Dict = (IDictionary<string, object?>)level3;
            level3Dict["finalValue"] = "deeply-nested-value";
            
            level2Dict["level3"] = level3;
            level1Dict["level2"] = level2;
            dict["level1"] = level1;
            
            // Act
            var result = InvokeGetPropertyValue(expando, "level1/level2/level3/finalValue");
            
            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("deeply-nested-value", result);
        }

        [TestMethod]
        public void GetPropertyValue_WithMissingNestedProperty_ReturnsNull()
        {
            // Arrange
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            dict["id"] = "test-id";
            
            var nestedExpando = new ExpandoObject();
            var nestedDict = (IDictionary<string, object?>)nestedExpando;
            nestedDict["existingKey"] = "value";
            
            dict["parent"] = nestedExpando;
            
            // Act
            var result = InvokeGetPropertyValue(expando, "parent/nonExistentKey");
            
            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetPropertyValue_WithNullIntermediateValue_ReturnsNull()
        {
            // Arrange
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            dict["id"] = "test-id";
            dict["parent"] = null;
            
            // Act
            var result = InvokeGetPropertyValue(expando, "parent/child");
            
            // Assert
            Assert.IsNull(result);
        }
    }
}
