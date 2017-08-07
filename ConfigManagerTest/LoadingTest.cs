﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using ConfigManager;
using System;
using System.Collections.Generic;

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
            Assert.AreEqual("value", ConfigTestTools.GetData(value), "Should have a data after key");
        }

        [TestMethod]
        public void TestLoadMultipleValues()
        {
            var config = LoadValidConfig("key 1\nkey 2");

            Assert.AreEqual(1, config.GetKeys().Length, "1 value should be loaded");

            List<ConfigValue> values = config.GetAll(config.GetKeys()[0]);
            Assert.IsNotNull(values, "Should get all existing values from key");
            Assert.AreEqual(2, values.Count, "Should get exactly 2 values for key");
            Assert.AreEqual("1", ConfigTestTools.GetData(values[0]), "Should have a data after key 1");
            Assert.AreEqual("2", ConfigTestTools.GetData(values[1]), "Should have a data after key 1");
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
