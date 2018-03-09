using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ConfigManager
{

    public static partial class Config
    {
        #region Method links
        private static readonly MethodInfo MethodConvertFromClass
            = typeof(Config)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.Name == "ConvertFromClass");
        private static readonly MethodInfo MethodConvertFromCollection
            = typeof(Config)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .First(m => m.Name == "ConvertFromCollection");
        private static readonly MethodInfo MethodCvDataFromCustom
            = typeof(Config)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .First(m => m.Name == "CvDataFromCustom");
        #endregion

        public static ConfigValue ConvertFromClass<TC>(TC instance)
            where TC : class, new()
        {
            if (instance == null) { return null; }

            if (typeof(TC).GetInterfaces().Contains(typeof(ICollection)))
            {
                return ConvertFromCollectionWrap(instance);
            }

            var config = Create();
            foreach (var field in typeof(TC).GetFields())
            {
                var dataSourceAttribute = field.GetCustomAttribute<ConfigDataSourceAttribute>();
                var path = dataSourceAttribute?.DataPath
                            ?? field.Name.ToLowerInvariant();

                var fieldValue = field.GetValue(instance);
                if (fieldValue == null) { continue; }

                if (field.FieldType.GetTypeInfo().IsPrimitive
                    || field.FieldType.GetConstructor(Type.EmptyTypes) == null)
                {
                    var genericConverter = MethodCvDataFromCustom.MakeGenericMethod(field.FieldType);
                    var value = (ConfigValue)genericConverter.Invoke(
                        null,
                        new[] { fieldValue }
                    );
                    config.SetByPath(path, value);
                }
                else
                {
                    var fieldType = field.FieldType;
                    var typeSource = field.GetCustomAttribute<ConfigDataTypeSourceAttribute>();

                    if (typeSource != null)
                    {
                        var mapping = field.GetCustomAttributes<ConfigDataTypeMappingAttribute>();
                        var typePath = mapping
                            ?.FirstOrDefault(attr => attr.FieldType == fieldType)
                            ?.TypeName;

                        if (string.IsNullOrEmpty(typePath))
                        {
                            throw new TypeLoadException(
                                $"Can not find name for type '{fieldType.FullName}'"
                            );
                        }
                        config.SetByPath(typePath, new ConfigValue(typePath));
                    }

                    var genericConverter = MethodConvertFromClass.MakeGenericMethod(fieldType);
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

        private static ConfigValue ConvertFromCollectionWrap<T>(T collection)
            where T : class, new()
        {
            var elementsType = typeof(T).GetGenericArguments().Single();
            var genericConverter = MethodConvertFromCollection.MakeGenericMethod(elementsType);

            return (ConfigValue)genericConverter.Invoke(null, new object[] { collection });
        }

        private static ConfigValue ConvertFromCollection<TE>(IEnumerable<TE> collection)
        {
            var elementType = typeof(TE);
            var config = Create();

            if (elementType.GetTypeInfo().IsPrimitive
                || elementType.GetConstructor(Type.EmptyTypes) == null)
            {
                foreach (var value in collection)
                {
                    config.Set(":", CvDataFromCustom(value));
                }
            }
            else
            {
                var genericConverter = MethodConvertFromClass.MakeGenericMethod(elementType);

                foreach (var value in collection)
                {
                    config.Set(":", (ConfigValue)genericConverter.Invoke(null, new object[] { value }));
                }
            }

            return config;
        }

        private static ConfigValue CvDataFromCustom<T>(T value)
        {
            var str = value.ToString();
            var escaped = ConfigValue.EscapeString(str);

            if (str.Count(char.IsWhiteSpace) != 0
                || escaped != str
                || string.IsNullOrEmpty(escaped))
            {
                escaped = $"\"{escaped}\"";
            }

            return new ConfigValue(escaped);
        }
    }
}
