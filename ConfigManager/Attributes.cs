using System;

namespace ConfigManager
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ConfigDataSourceAttribute : Attribute
    {
        /// <summary>
        /// Holds Config value path
        /// </summary>
        public string DataPath { get; }

        /// <summary>
        /// Specifies path from wich data will be loaded into target field
        /// </summary>
        /// <param name="dataPath">Path to value(-s)</param>
        public ConfigDataSourceAttribute(string dataPath)
        {
            DataPath = dataPath?.ToLowerInvariant();
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ConfigDataTypeSourceAttribute : Attribute
    {
        /// <summary>
        /// Holds Value type path 
        /// </summary>
        public string DataPath { get; }

        /// <summary>
        /// Specifies path from wich type name will be loaded
        /// </summary>
        /// <param name="dataPath">Path to type name</param>
        public ConfigDataTypeSourceAttribute(string dataPath)
        {
            DataPath = dataPath?.ToLowerInvariant();
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class ConfigDataTypeMappingAttribute : Attribute
    {
        /// <summary>
        /// Holds custom name for given type
        /// </summary>
        public string TypeName { get; }
        /// <summary>
        /// Holds custom Type associated with gien name
        /// </summary>
        public Type FieldType { get; }

        /// <summary>
        /// Specifies custom name for type
        /// </summary>
        /// <param name="name">Custom type name</param>
        /// <param name="type">Target type</param>
        public ConfigDataTypeMappingAttribute(string name, Type type)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(nameof(name));
            }
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            TypeName = name.ToLowerInvariant();
            FieldType = type;
        }
    }
}
