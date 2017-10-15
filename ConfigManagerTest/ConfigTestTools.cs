using ConfigManager;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConfigManagerTest
{
    public static class ConfigTestTools
    {
        private static readonly FieldInfo ConfigValueDataField;
        static ConfigTestTools()
        {
            ConfigValueDataField = typeof(ConfigValue).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static string GetData(ConfigValue cv)
        {
            return (string)ConfigValueDataField.GetValue(cv);
        }
        
        public static ConfigValue LoadValidConfig(string data)
        {
            var config = Config.Load(data);

            Assert.IsNotNull(config, "Empty config should be valid");
            Assert.IsNull(GetData(config), "Config top level data should be null");
            Assert.IsNotNull(config.AsConfigList(), "Config should always have parsed values list");
            Assert.AreEqual(0, config.AsConfigList().Count, "Config should have no data values");

            return config;
        }

        public static T LoadValidConfig<T>(string data)
            where T: class, new()
        {
            var cfg = LoadValidConfig(data);
            var cls = Config.LoadToClass<T>(cfg);
            
            Assert.IsNotNull(cls, "Valid config should produce class with valid state");
            Assert.IsInstanceOfType(cls, typeof(T), "Class types should match");

            return cls;
        }
    }
}
