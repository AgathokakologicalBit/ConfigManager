using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ConfigManager
{
    public static partial class Config
    {
        #region Method links
        private static readonly MethodInfo MethodLoadToClass
            = typeof(Config).GetMethod("LoadToClass", new[] { typeof(ConfigValue) });
        private static readonly MethodInfo MethodLoadToCollection
            = typeof(Config)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .First(m => m.Name == "LoadToCollection");
        private static readonly MethodInfo MethodAsCustom
            = typeof(ConfigValue).GetMethod("AsCustom");
        private static readonly MethodInfo MethodAsCustomFromRaw
            = typeof(ConfigValue).GetMethod("AsCustomFromRaw");
        #endregion

        #region State
        private class State
        {
            public int Line { get; set; }
            public string[] Data { get; }

            public Stack<ConfigValue> Context { get; }

            public State(string[] data)
            {
                Line = 0;
                Data = data;
                Context = new Stack<ConfigValue>();
            }
        }
        #endregion

        #region Loaders
        /// <summary>
        /// Loads configuration from given file
        /// </summary>
        /// <param name="filename">Configuration file</param>
        /// <returns>Parsed configuration tree</returns>
        public static ConfigValue LoadFromFile(string filename)
        {
            try
            {
                return Load(File.ReadAllText(filename));
            }
            catch (FileNotFoundException)
            {
                return Create();
            }
        }

        /// <summary>
        /// Loads configuration from given text
        /// </summary>
        /// <param name="data">Configuration text</param>
        /// <returns>Parsed configuration tree</returns>
        public static ConfigValue Load(string data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var state = new State(data.Split('\n'));

            var coreConfigValue = Create();
            state.Context.Push(coreConfigValue);

            ParseLevel(state, "");

            return coreConfigValue;
        }

        /// <summary>
        /// Loads configuration to given Class type
        /// </summary>
        /// <typeparam name="T">Target class</typeparam>
        /// <param name="data">Configuration text</param>
        /// <returns>Class, filled with configuration values</returns>
        public static T LoadToClass<T>(string data)
            where T : class, new()
        {
            return LoadToClass<T>(Load(data));
        }

        /// <summary>
        /// Loads configuration to given Class type from given file
        /// </summary>
        /// <typeparam name="T">Target class</typeparam>
        /// <param name="filename">Configuration file</param>
        /// <returns></returns>
        public static T LoadToClassFromFile<T>(string filename)
            where T : class, new()
        {
            return LoadToClass<T>(LoadFromFile(filename));
        }

        /// <summary>
        /// Loads configuration to given Class type
        /// </summary>
        /// <typeparam name="T">Target class</typeparam>
        /// <param name="config">Configuration</param>
        /// <returns>Class, filled with configuration values oe null</returns>
        public static T LoadToClass<T>(ConfigValue config)
            where T : class, new()
        {
            if (config == null)
            {
                return null;
            }

            if (typeof(T).GetInterfaces().Contains(typeof(ICollection)))
            {
                return LoadToCollectionWrap<T>(config);
            }

            var instance = new T();
            foreach (var field in typeof(T).GetFields())
            {
                var dataSourceAttribute = field.GetCustomAttribute<ConfigDataSourceAttribute>();
                var path = dataSourceAttribute?.DataPath
                    ?? field.Name.ToLowerInvariant();

                if (!config.ContainsPath(path))
                {
                    // Skipping not relevant fields
                    continue;
                }

                if (field.FieldType.GetTypeInfo().IsPrimitive
                    || field.FieldType.GetConstructor(Type.EmptyTypes) == null)
                {
                    var configValue = config.GetByPath(path);
                    var genericAsCustom =
                        configValue.AsConfigList().Count == 1
                            ? MethodAsCustom.MakeGenericMethod(field.FieldType)
                            : MethodAsCustomFromRaw.MakeGenericMethod(field.FieldType);

                    var value = genericAsCustom.Invoke(configValue, null);

                    field.SetValue(instance, value);
                }
                else
                {
                    var fieldType = field.FieldType;

                    var typeSource = field.GetCustomAttribute<ConfigDataTypeSourceAttribute>();
                    var typeName =
                        config.GetByPath(typeSource?.DataPath)
                        ?.AsString()
                        ?.ToLowerInvariant();
                    if (typeName != null)
                    {
                        var mapping = field.GetCustomAttributes<ConfigDataTypeMappingAttribute>();
                        fieldType = mapping
                                        ?.FirstOrDefault(attr => attr.TypeName == typeName)
                                        ?.FieldType
                                    ?? Assembly
                                        .GetEntryAssembly()
                                        .GetTypes()
                                        .Where(t => field.FieldType.IsAssignableFrom(t))
                                        .FirstOrDefault(t => t.Name.ToLowerInvariant() == typeName)
                                    ?? throw new TypeLoadException(
                                        $"Can not find type with name '{typeName}'"
                                    );
                    }

                    var genericLoadToClass = MethodLoadToClass.MakeGenericMethod(fieldType);
                    var innerInstance = genericLoadToClass.Invoke(null, new object[] { config.GetByPath(path) });
                    field.SetValue(instance, innerInstance);
                }
            }

            return instance;
        }

        private static T LoadToCollectionWrap<T>(ConfigValue config)
            where T : class, new()
        {
            var keys = config.GetKeys();
            if (keys.Length > 1)
            {
                throw new FormatException("Array can only contains values with the same identifier");
            }
            else if (keys.Length == 0)
            {
                return new T();
            }

            var elementsType = typeof(T).GetGenericArguments().Single();
            var genericLoadToCollection = MethodLoadToCollection.MakeGenericMethod(typeof(T), elementsType);

            return (T)genericLoadToCollection.Invoke(null, new object[] { config.GetAll(keys[0]) });
        }

        private static T LoadToCollection<T, TE>(IEnumerable<ConfigValue> configValues)
            where T : class, ICollection, new()
        {
            var elementType = typeof(TE);
            var collection = (ICollection<TE>)new T();

            if (elementType.GetTypeInfo().IsPrimitive
                || elementType.GetConstructor(Type.EmptyTypes) == null)
            {
                foreach (var value in configValues)
                {
                    collection.Add(
                        value.AsConfigList().Count == 1
                            ? value.AsCustom<TE>()
                            : value.AsCustomFromRaw<TE>()
                    );
                }
            }
            else
            {
                var genericLoadToClass = MethodLoadToClass.MakeGenericMethod(elementType);

                foreach (var value in configValues)
                {
                    collection.Add((TE)genericLoadToClass.Invoke(null, new[] { value }));
                }
            }

            return (T)collection;
        }
        #endregion

        #region Parsers
        private static void ParseLevel(State state, string baseIndentation)
        {
            ConfigValue lastConfigValue = null;
            while (state.Line < state.Data.Length)
            {
                var line = state.Data[state.Line].TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    state.Line += 1;
                    continue;
                }

                var lineIndentation = GetIndentation(line);
                ValidateIndentationLevel(state, lineIndentation, baseIndentation);

                if (lineIndentation.Length > baseIndentation.Length)
                {
                    state.Context.Push(lastConfigValue);
                    ParseLevel(state, lineIndentation);
                    continue;
                }

                if (lineIndentation.Length < baseIndentation.Length)
                {
                    state.Context.Pop();
                    break;
                }

                var data = line.Substring(lineIndentation.Length);
                var len = data.IndexOfAny(new[] { ' ', '\t' });

                var name = len < 0 ? data : data.Substring(0, len);
                var value = len < 0 ? "" : data.Substring(len).Trim();
                lastConfigValue = new ConfigValue(value);
                state.Context.Peek().Set(
                    name,
                    lastConfigValue
                );
                state.Line += 1;
            }
        }


        private static void ValidateIndentationLevel
            (State state, string lineIndentation, string baseIndentation)
        {
            if (lineIndentation.Length > baseIndentation.Length
                && state.Line < 1)
            {
                throw new FormatException(
                    $"Invalid root indentation level({lineIndentation.Length})\n" +
                    "Expected no indentation at the beginning of config"
                );
            }

            if (lineIndentation.Length == baseIndentation.Length
                && lineIndentation != baseIndentation)
            {
                throw new FormatException(
                    $"Indentation levels doesn't match on line {state.Line}\n" +
                    "Consider rechecking spaces and tabulations in text:\n"
                );
            }
        }

        private static string GetIndentation(string line)
        {
            var length = line.TakeWhile(char.IsWhiteSpace).Count();
            return line.Substring(0, length);
        }
        #endregion
    }
}
