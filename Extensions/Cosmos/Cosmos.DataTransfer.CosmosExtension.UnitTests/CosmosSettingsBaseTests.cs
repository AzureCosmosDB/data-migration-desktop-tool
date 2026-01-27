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

        [TestMethod]
        public void EnableContentResponseOnWrite_Property_ShouldSetAndGet()
        {
            var settings = new TestableCosmosSettings
            {
                EnableContentResponseOnWrite = false
            };

            Assert.IsFalse(settings.EnableContentResponseOnWrite, "EnableContentResponseOnWrite should be false when set to false");

            settings.EnableContentResponseOnWrite = true;
            Assert.IsTrue(settings.EnableContentResponseOnWrite, "EnableContentResponseOnWrite should be true when set to true");
        }

        [TestMethod]
        public void EnableContentResponseOnWrite_Property_ShouldDefaultToTrue()
        {
            var settings = new TestableCosmosSettings();

            Assert.IsTrue(settings.EnableContentResponseOnWrite, "EnableContentResponseOnWrite should default to true");
        }
    }
}
