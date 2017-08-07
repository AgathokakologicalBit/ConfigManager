using ConfigManager;
using System.Reflection;

namespace ConfigManagerTest
{
    public static class ConfigTestTools
    {
        private static FieldInfo configValueDataField;
        static ConfigTestTools()
        {
            configValueDataField = typeof(ConfigValue).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static string GetData(ConfigValue cv)
        {
            return (string)configValueDataField.GetValue(cv);
        }
    }
}
