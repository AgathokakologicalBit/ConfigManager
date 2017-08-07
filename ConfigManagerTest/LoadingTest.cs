using Microsoft.VisualStudio.TestTools.UnitTesting;
using ConfigManager;
using System;

namespace ConfigManagerTest
{
    [TestClass]
    public class LoadingTest
    {
        [TestMethod]
        public void TestLoadEmpty()
        {
            var config = LoadValidConfig("");

            Assert.AreEqual(0, config.GetKeys().Length, "Config should have no enclosed values by default");
        }

        [TestMethod]
        public void TestLoadValue()
        {
            var config = LoadValidConfig("key value");

            Assert.AreEqual(1, config.GetKeys().Length, "1 value should be loaded");

            ConfigValue value = config.Get(config.GetKeys()[0]);
            Assert.IsNotNull(value, "Should get existing key");
            Assert.AreEqual("value", ConfigTestTools.GetData(value), "Should have data after key");
        }

        private static ConfigValue LoadValidConfig(string data)
        {
            ConfigValue config = Config.Load(data);

            Assert.IsNotNull(config, "Empty config should be valid");
            Assert.IsNull(ConfigTestTools.GetData(config), "Config top level data should be null");
            Assert.IsNotNull(config.AsConfigList(), "Config should always have parsed values list");
            Assert.AreEqual(0, config.AsConfigList().Count, "Config should have no data values");

            return config;
        }
    }
}
