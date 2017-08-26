using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ConfigManager
{
    public static partial class Config
    {
        #region Method links
        private static readonly MethodInfo methodConvertFromClass
            = typeof(Config)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.Name == "ConvertFromClass");
        private static readonly MethodInfo methodConvertFromCollection
            = typeof(Config).GetMethod("ConvertFromCollection");
        private static readonly MethodInfo methodCVDataFromCustom
            = typeof(Config)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .First(m => m.Name == "CVDataFromCustom");
        #endregion


        public static ConfigValue ConvertFromClass<C>(C instance)
            where C: class, new()
        {
            if (instance == null) { return null; }

            if (typeof(C).GetInterfaces().Contains(typeof(ICollection)))
            {
                return ConvertFromCollectionWrap(instance, "array");
            }
            if (instance == null) { return null; }

            var config = Config.Create();
            foreach (FieldInfo field in typeof(C).GetFields())
            {
                string path = field.Name.ToLowerInvariant();
                var dataSourceAttribute = field.GetCustomAttribute<ConfigDataSourceAttribute>();
                if (dataSourceAttribute != null)
                {
                    path = dataSourceAttribute.DataPath;
                }

                var fieldValue = field.GetValue(instance);
                if (fieldValue == null) { continue; }

                if (field.FieldType.IsSerializable
                    || field.FieldType.GetConstructor(Type.EmptyTypes) == null)
                {
                    var genericConverter = methodCVDataFromCustom.MakeGenericMethod(field.FieldType);
                    
                    ConfigValue value = (ConfigValue)genericConverter.Invoke(
                        null,
                        new[] { fieldValue }
                    );
                    config.SetByPath(path, value);
                }
                else
                {
                    var fieldType = field.FieldType;

                    var typeSource = field.GetCustomAttribute<ConfigDataTypeSourceAttribute>();
                    var typePath = fieldType.Name;
                    if (typeSource != null)
                    {
                        var mapping = field.GetCustomAttributes<ConfigDataTypeMappingAttribute>();
                        typePath = mapping?.FirstOrDefault(attr => attr.FieldType == fieldType)?.TypeName;

                        if (String.IsNullOrEmpty(typePath))
                        {
                            throw new TypeLoadException(
                                $"Can not find name for type '{fieldType.FullName}'"
                            );
                        }
                        config.SetByPath(typePath, new ConfigValue(typePath));
                    }

                    var genericConverter = methodConvertFromClass.MakeGenericMethod(fieldType);
                    
                    var innerInstance = (ConfigValue)genericConverter.Invoke(
                        null,
                        new[] { fieldValue }
                    );
                    if (innerInstance == null) { continue; }

                    config.SetByPath(path, innerInstance);
                }
            }

            return config;
        }

        private static ConfigValue ConvertFromCollectionWrap<T>(T collection, string name)
            where T : class, new()
        {
            var elementsType = typeof(T).GetGenericArguments().Single();
            var genericConverter = methodConvertFromCollection.MakeGenericMethod(typeof(T), elementsType);

            return (ConfigValue)genericConverter.Invoke(null, new object[] { collection, name });
        }

        private static ConfigValue ConvertFromCollection<T, E>(ICollection<E> collection, string name)
            where T : class, ICollection
        {
            var elementType = typeof(E);
            var config = Config.Create();

            if (elementType.IsSerializable
                || elementType.GetConstructor(Type.EmptyTypes) == null)
            {
                foreach (E value in collection)
                {
                    if (value == null) { continue; }
                    config.Set(":", CVDataFromCustom(value));
                }
            }
            else
            {
                var genericConverter = methodConvertFromClass.MakeGenericMethod(elementType);

                foreach (E value in collection)
                {
                    if (value == null) { continue; }
                    config.Set(":", (ConfigValue)genericConverter.Invoke(null, new object[] { value }));
                }
            }

            return config;
        }

        private static ConfigValue CVDataFromCustom<T>(T value)
        {
            return new ConfigValue(value.ToString());
        }
    }
}
