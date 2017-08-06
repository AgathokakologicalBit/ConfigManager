using System;

namespace ConfigManager
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ConfigDataSource : Attribute
    {
        public string DataPath { get; private set; } = "";

        public ConfigDataSource(string path)
        {
            if (path == null)
            {
                throw new Exception("Path can not be null");
            }

            DataPath = path.ToLower();
        }
    }
}
