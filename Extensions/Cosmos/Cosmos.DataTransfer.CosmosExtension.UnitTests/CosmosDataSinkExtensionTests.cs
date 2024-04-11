using Cosmos.DataTransfer.Interfaces;
using System.Dynamic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests
{
    [TestClass]
    public class CosmosDataSinkExtensionTests
    {
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
    }
}
