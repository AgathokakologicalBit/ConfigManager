using System;

namespace ConfigManager
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ConfigDataSourceAttribute : Attribute
    {
        /// <summary>
        /// Holds Config value path
        /// </summary>
        public string DataPath { get; private set; } = "";

        /// <summary>
        /// Specifies path from wich data will be loaded into target field
        /// </summary>
        /// <param name="dataPath">Path to value(-s)</param>
        public ConfigDataSourceAttribute(string dataPath)
        {
            DataPath = dataPath?.ToLowerInvariant();
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ConfigDataTypeSourceAttribute : Attribute
    {
        /// <summary>
        /// Holds Value type path 
        /// </summary>
        public string DataPath { get; private set; } = "";

        /// <summary>
        /// Specifies path from wich type name will be loaded
        /// </summary>
        /// <param name="dataPath">Path to type name</param>
        public ConfigDataTypeSourceAttribute(string dataPath)
        {
            DataPath = dataPath?.ToLowerInvariant();
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public sealed class ConfigDataTypeMappingAttribute : Attribute
    {
        /// <summary>
        /// Holds custom name for given type
        /// </summary>
        public string TypeName { get; private set; }
        /// <summary>
        /// Holds custom Type associated with gien name
        /// </summary>
        public Type FieldType { get; private set; }

        /// <summary>
        /// Specifies custom name for type
        /// </summary>
        /// <param name="name">Custom type name</param>
        /// <param name="type">Target type</param>
        public ConfigDataTypeMappingAttribute(string name, Type type)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name can not be empty", "name");
            }
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            TypeName = name.ToLowerInvariant();
            FieldType = type;
        }
    }
}
