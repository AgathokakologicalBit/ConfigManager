using System;

namespace ConfigManager
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ConfigDataSource : Attribute
    {
        /// <summary>
        /// Hold Config value path as data source
        /// </summary>
        public string DataPath { get; private set; } = "";

        /// <summary>
        /// Specifies path from wich data will be loaded into target field
        /// </summary>
        /// <param name="path">Path to value(-s)</param>
        public ConfigDataSource(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("Path can not be null");
            }

            DataPath = path.ToLowerInvariant();
        }
    }
}
