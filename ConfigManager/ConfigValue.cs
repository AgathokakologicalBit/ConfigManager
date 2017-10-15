using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ConfigManager
{
    public class ConfigValue
    {
        #region Data
        private readonly Dictionary<string, List<ConfigValue>> _values;
        private string _data;
        private List<ConfigValue> _parsedData;

        private static readonly IReadOnlyDictionary<string, string> SwapList
            = new Dictionary<string, string>
            {
                { @"\\", @"\" },
                { @"\n", "\n" },
                { @"\t", "\t" },
                { "\\\"", "\"" }
            };
        private static readonly Regex RegexEscapeSwap
            = new Regex(string.Join("|", SwapList.Keys.Select(k => k.Replace("\\", "\\\\"))));
        private static readonly Regex RegexUnescapeSwap
            = new Regex(string.Join("|", SwapList.Values.Select(v => v.Replace("\\", "\\\\"))));
        #endregion

        #region Initialisation methods

        internal ConfigValue(string data, bool final = false)
        {
            _data = data;
            _values = new Dictionary<string, List<ConfigValue>>();

            _parsedData = final ? new List<ConfigValue> { this } : ParseData(_data);
        }


        #region Parsing
        private static List<ConfigValue> ParseData(string data)
        {
            var dataParsed = new List<ConfigValue>();
            if (string.IsNullOrWhiteSpace(data))
            {
                return dataParsed;
            }

            var from = 0;
            var index = 0;
            while (index < data.Length)
            {
                if (data[index] == '"')
                {
                    dataParsed.Add(new ConfigValue(ParseString(data, ref index), true));
                    continue;
                }

                while (index < data.Length && !char.IsWhiteSpace(data[index]))
                {
                    index += 1;
                }

                dataParsed.Add(new ConfigValue(data.Substring(from, index - from), true));
                while (index < data.Length && char.IsWhiteSpace(data[index]))
                {
                    index += 1;
                }

                from = index;
            }

            return dataParsed;
        }

        private static string ParseString(string data, ref int index)
        {
            var from = ++index;

            while (index < data.Length)
            {
                if (data[index] == '"')
                {
                    return UnescapeString(data.Substring(from, index++ - from));
                }

                if (data[index++] == '\\')
                {
                    ++index;
                }
            }

            throw new FormatException("String does not contains closing character");
        }
        #endregion
        #endregion

        #region Getters/Setters
        #region Getters
        /// <summary>
        /// Gets list of all saved values with specified name.
        /// 
        /// Returns null if specified name does not exists.
        /// </summary>
        /// <param name="name">Value identifier</param>
        /// <returns>List of saved values</returns>
        public IReadOnlyList<ConfigValue> GetAll(string name)
        {
            if (name == null)
            {
                return null;
            }

            _values.TryGetValue(name.ToLowerInvariant(), out var values);
            return values?.AsReadOnly() ?? new List<ConfigValue>().AsReadOnly();
        }

        /// <summary>
        /// Gets N-th saved value with specified name.
        /// Gets first saved value by default.
        /// 
        /// Returns null if specified name does not exists.
        /// Returns null if specified index is out of range.
        /// </summary>
        /// <param name="name">Value identifier</param>
        /// <param name="index">Target value index</param>
        /// <returns>Config value or null</returns>
        public ConfigValue Get(string name, int index = 0)
        {
            return GetAll(name)?.ElementAtOrDefault(index);
        }

        /// <summary>
        /// Gets N-th saved value with specified name.
        /// Gets first saved value by default.
        /// 
        /// Returns default value if specified name does not exists.
        /// Returns default value if specified index is out of range.
        /// </summary>
        /// <param name="name">Value identifier</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="index">Target value index</param>
        /// <returns>Config value or Default value</returns>
        public ConfigValue GetOrDefault<T>(string name, T defaultValue, int index = 0)
        {
            return Get(name, index) ?? ConfigValueFromCustom(defaultValue);
        }

        /// <summary>
        /// Gets all values by specified path.
        /// </summary>
        /// <param name="path">Path to values</param>
        /// <returns>Config values list</returns>
        public IReadOnlyList<ConfigValue> GetAllByPath(string path)
        {
            if (path == null)
            {
                return null;
            }

            var targets = new List<ConfigValue> { this };
            if (string.IsNullOrWhiteSpace(path))
            {
                return targets;
            }

            var pathLower = path.Trim().ToLowerInvariant();
            var position = 0;
            while (position < pathLower.Length)
            {
                if (char.IsDigit(pathLower[position]))
                {
                    var indexStr = new string(
                        pathLower.Skip(position).TakeWhile(char.IsDigit).ToArray()
                    );
                    var index = int.Parse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    var newTarget = targets.ElementAtOrDefault(index);
                    if (newTarget == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets.Clear();
                    targets.Add(newTarget);
                    position += indexStr.Length;
                }
                else if (char.IsLetter(pathLower[position]))
                {
                    var key = new string(
                        pathLower.Skip(position).TakeWhile(char.IsLetter).ToArray()
                    );
                    var newTargetsList = targets.FirstOrDefault()?.GetAll(key);
                    if (newTargetsList == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets.Clear();
                    foreach (var newTarget in newTargetsList)
                    {
                        targets.Add(newTarget);
                    }
                    position += key.Length;
                }
                else if (pathLower[position] == '.')
                {
                    position += 1;
                }
                else if (pathLower[position] == '$' && char.IsDigit(pathLower.ElementAtOrDefault(position + 1)))
                {
                    var indexStr = new string(
                        pathLower.Skip(position).TakeWhile(c => char.IsDigit(c) || c == '$').ToArray()
                    );
                    var index = int.Parse(indexStr.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    var target = targets[0];
                    var newTarget = target.AsConfigArray().ElementAtOrDefault(index);
                    if (newTarget == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets.Clear();
                    targets.Add(newTarget);
                    position += indexStr.Length;
                    if (position < pathLower.Length)
                    {
                        throw new FormatException(
                            $"Data arguments indexation must be the last operation. Rest: '{pathLower}'"
                        );
                    }
                }
                else
                {
                    throw new FormatException($"Unexpected symbol '{pathLower[0]}'({(byte)pathLower[0]})");
                }

                if (targets.Count == 0) { return targets; }
            }

            return targets.AsReadOnly();
        }

        /// <summary>
        /// Gets value by specified path.
        /// 
        /// Returns null if specified value does not exists.
        /// </summary>
        /// <param name="path">Path to value</param>
        /// <returns>Config value or null</returns>
        public ConfigValue GetByPath(string path)
        {
            return GetAllByPath(path)?.FirstOrDefault();
        }

        /// <summary>
        /// Gets value by specified path.
        /// 
        /// Returns default value if specified one does not exists.
        /// </summary>
        /// <param name="path">Path to value</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Config value or Default value</returns>
        public ConfigValue GetOrDefaultByPath<T>(string path, T defaultValue)
        {
            return GetByPath(path) ?? ConfigValueFromCustom(defaultValue);
        }

        /// <summary>
        /// Gets an array of saved values identifiers (Dictionary keys)
        /// </summary>
        /// <returns>Array of keys</returns>
        public string[] GetKeys()
        {
            return new List<string>(_values.Keys).ToArray();
        }
        #endregion

        #region Setters
        /// <summary>
        /// Saves new value with given name
        /// Or adds it to existing list if name already taken.
        /// To prevent adding value to the end of list use "index = 0". It will rewrite the existing one.
        /// 
        /// If index is equal to -1 value will be added at the end of list.
        /// </summary>
        /// <param name="name">Value identifier</param>
        /// <param name="value">Config value</param>
        /// <param name="index">Target index in list</param>
        public ConfigValue Set(string name, ConfigValue value, int index = -1)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (value == null) { return this; }

            var nameLower = name.ToLowerInvariant();
            if (_values.ContainsKey(nameLower))
            {
                if (index == -1) { _values[nameLower].Add(value); }
                else { _values[nameLower][index] = value; }
            }
            else
            {
                _values[nameLower] = new List<ConfigValue>(new[] { value });
            }

            return this;
        }

        /// <summary>
        /// Saves new value with given name
        /// Or adds it to existing list if name already taken.
        /// To prevent adding value to the end of list use "index = 0". It will rewrite the existing one.
        /// 
        /// Value must be convertible to string and back in order to be saved in config.
        /// 
        /// If index is equal to -1 value will be added at the end of list.
        /// </summary>
        /// <typeparam name="T">Type of value to save</typeparam>
        /// <param name="name">Value identifier</param>
        /// <param name="value">Custom value</param>
        /// <param name="index">Target index in list</param>
        public void Set<T>(string name, T value, int index = -1)
        {
            Set(name, ConfigValueFromCustom(value), index);
        }

        /// <summary>
        /// Saves new values with given name
        /// </summary>
        /// <param name="path">Path to the value</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public ConfigValue SetByPath(string path, ConfigValue value)
        {
            SetByPathNamed(path, this, value);
            return this;
        }

        private static void SetByPathNamed
            (string path, ConfigValue root, ConfigValue value)
        {
            if (path == null || value == null) { return; }

            var pathLower = path.ToLowerInvariant().Trim().TrimStart('.');
            var targetParent = root;
            var target = root;
            var name = ":";

            while (!string.IsNullOrEmpty(pathLower)) {
                if (char.IsDigit(pathLower.FirstOrDefault()))
                {
                    var indexStr = new string(
                        pathLower.TakeWhile(char.IsDigit).ToArray()
                    );

                    var sindex = int.Parse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture);

                    while (targetParent.GetAll(name).Count <= sindex)
                    {
                        targetParent.Set(name, "");
                    }

                    pathLower = pathLower.Substring(indexStr.Length).Trim().TrimStart('.');
                    target = targetParent.GetAll(name)[sindex];
                }
                else if (char.IsLetter(pathLower.First()))
                {
                    var key = new string(
                        pathLower.TakeWhile(char.IsLetter).ToArray()
                    );

                    pathLower = pathLower.Substring(key.Length).Trim().TrimStart('.');
                    targetParent = target;
                    if (!target.Contains(key))
                    {
                        target.Set(key, Config.Create());
                    }
                    target = target.Get(key);
                    name = key;
                }
                else if (pathLower.First() == '$' && char.IsDigit(pathLower.ElementAtOrDefault(1)))
                {
                    var indexStr = new string(
                        pathLower.Skip(1).TakeWhile(char.IsDigit).ToArray()
                    );
                    var sindex = int.Parse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    target.InsertIntoData(sindex, value._parsedData.First()._data);
                    target._data = target.ParsedDataToRawString();

                    pathLower = pathLower.Substring(indexStr.Length + 1).Trim().TrimStart('.');
                    if (!string.IsNullOrEmpty(pathLower))
                    {
                        throw new FormatException(
                            $"Data arguments indexation must be the last operation. Rest: '{pathLower}'"
                        );
                    }
                    return;
                }
                else
                {
                    throw new FormatException($"Unexpected symbol '{pathLower[0]}'({(byte)pathLower[0]})");
                }
            }


            if (value._data != null)
            {
                target._data = value._data;
                target._parsedData = value._parsedData;
            }

            foreach (var val in value._values)
            {
                target._values.Add(val.Key, val.Value);
            }
        }
        #endregion
        #endregion

        #region Special CFGV methods
        /// <summary>
        /// Checks whether given value name is exists on current level of config.
        /// </summary>
        /// <param name="name">Value identifier</param>
        /// <returns>Is config contains given identifier</returns>
        public bool Contains(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _values.ContainsKey(name.ToLowerInvariant());
        }

        /// <summary>
        /// Checks whether any value can be located by path
        /// </summary>
        /// <param name="path">Path to value</param>
        /// <returns>Is config contains any value with given path</returns>
        public bool ContainsPath(string path)
        {
            return path != null && GetAllByPath(path).Count > 0;
        }
        #endregion

        #region Convertion methods
        /// <summary>
        /// Gets data as raw string
        /// </summary>
        /// <returns>String data</returns>
        public string AsRawString() => _data;

        /// <summary>
        /// Gets data as parsed string
        /// </summary>
        /// <returns>String data</returns>
        public string AsString() => _parsedData[0]._data;
        /// <summary>
        /// Gets data as escaped parsed string
        /// </summary>
        /// <returns>Escaped string data</returns>
        public string AsEscapedString() => EscapeString(AsString());

        /// <summary>
        /// Gets data as boolean value
        /// </summary>
        /// <returns>Boolean value</returns>
        public bool AsBoolean() => bool.Parse(_parsedData[0]._data);

        /// <summary>
        /// Gets data as 32 bit integer(int) value
        /// </summary>
        /// <returns>Int32 value</returns>
        public int AsInt() => int.Parse(_parsedData[0]._data, NumberStyles.Integer, CultureInfo.InvariantCulture);
        /// <summary>
        /// Gets data as 64 bit integer(long) value
        /// </summary>
        /// <returns>Int64 value</returns>
        public long AsLong() => long.Parse(_parsedData[0]._data, NumberStyles.Integer, CultureInfo.InvariantCulture);
        /// <summary>
        /// Gets data as single precision floating point(float) value
        /// </summary>
        /// <returns>Single precision floating point value</returns>
        public float AsFloat() => float.Parse(_parsedData[0]._data, NumberStyles.Number, CultureInfo.InvariantCulture);
        /// <summary>
        /// Gets data as double precision floating point(double) value
        /// </summary>
        /// <returns>Double precision floating point value</returns>
        public double AsDouble() => double.Parse(_parsedData[0]._data, NumberStyles.Number, CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets data as a list of ConfigValues.
        /// data is splitted by whitespace.
        /// </summary>
        /// <returns>Data as a list of Config values</returns>
        public IReadOnlyList<ConfigValue> AsConfigList() => _parsedData;
        /// <summary>
        /// Gets data as an array of ConfigValues.
        /// data is splitted by whitespace.
        /// </summary>
        /// <returns>Data as an array of Config values</returns>
        public ConfigValue[] AsConfigArray() => _parsedData.ToArray();

        /// <summary>
        /// Gets data as a list of string.
        /// data is splitted by whitespace.
        /// </summary>
        /// <returns>Data as a list of string</returns>
        public IReadOnlyList<string> AsList() => _parsedData.Select(cv => cv.AsString()).ToList().AsReadOnly();
        /// <summary>
        /// Gets data as an array of strings.
        /// data is splitted by whitespace.
        /// </summary>
        /// <returns>Data as an array of strings</returns>
        public string[] AsArray() => _parsedData.Select(cv => cv.AsString()).ToArray();

        /// <summary>
        /// Gets data as value with specified type.
        /// 
        /// Throws NotSupportedException if data can not be converted to target type.
        /// </summary>
        /// <returns>Converted data</returns>
        public T AsCustom<T>()
        {
            var tc = TypeDescriptor.GetConverter(typeof(T));
            return (T)tc.ConvertFromString(_parsedData[0]._data);
        }

        /// <summary>
        /// Gets RAW data as value with specified type.
        /// 
        /// Throws NotSupportedException if data can not be converted to target type.
        /// </summary>
        /// <returns>Converted raw data</returns>
        public T AsCustomFromRaw<T>()
        {
            var tc = TypeDescriptor.GetConverter(typeof(T));
            return (T)tc.ConvertFromString(_data);
        }
        #endregion

        #region Special methods
        internal static string EscapeString(string data)
            => RegexUnescapeSwap.Replace(data ?? "", v => SwapList.First(x => x.Value == v.Value).Key);

        internal static string UnescapeString(string data)
            => RegexEscapeSwap.Replace(data ?? "", v => SwapList[v.Value]);

        internal static ConfigValue ConfigValueFromCustom<T>(T value)
        {
            var tc = TypeDescriptor.GetConverter(typeof(string));
            var stringValue = (string)tc.ConvertFrom(value);
            return new ConfigValue(stringValue);
        }

        private void InsertIntoData(int index, string value)
        {
            while (_parsedData.Count <= index)
            {
                _parsedData.Add(new ConfigValue(string.Empty, final: true));
            }
            _parsedData[index] = new ConfigValue(value, final: true);
        }

        private string ParsedDataToRawString()
        {
            var builder = new StringBuilder(_parsedData.Count * 2);

            for (var index = 0; index < _parsedData.Count; ++index)
            {
                if (index != 0) { builder.Append(" "); }

                var escaped = EscapeString(_parsedData[index]._data);
                if (escaped.Count(char.IsWhiteSpace) != 0
                    || escaped != _parsedData[index]._data
                    || string.IsNullOrEmpty(escaped)) {
                    escaped = $"\"{escaped}\"";
                }

                builder.Append(escaped);
            }

            return builder.ToString();
        }
        #endregion
    }
}
