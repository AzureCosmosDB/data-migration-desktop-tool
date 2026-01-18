using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.DataTransfer.CosmosExtension;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests
{
    [TestClass]
    public class CosmosSettingsBaseTests
    {
        private class TestableCosmosSettings : CosmosSettingsBase { }

        [TestMethod]
        public void AllowBulkExecution_Property_ShouldSetAndGet()
        {
            var settings = new TestableCosmosSettings
            {
                AllowBulkExecution = true
            };

            Assert.IsTrue(settings.AllowBulkExecution, "AllowBulkExecution should be true when set to true");

            settings.AllowBulkExecution = false;
            Assert.IsFalse(settings.AllowBulkExecution, "AllowBulkExecution should be false when set to false");
        }
    }
}
