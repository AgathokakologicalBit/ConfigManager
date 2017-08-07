using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConfigManager
{
    public class ConfigValue
    {
        #region Data
        private readonly Dictionary<string, List<ConfigValue>> _values;
        private readonly string _data;
        private readonly IReadOnlyList<ConfigValue> _parsedData;

        private static readonly IReadOnlyDictionary<string, string> swapList
            = new Dictionary<string, string>
            {
                { @"\\", @"\" },
                { @"\n", "\n" },
                { @"\t", "\t" },
                { @"\s", " " },
                { "\\\"", "\"" }
            };
        private static readonly Regex regexEscapeSwap
            = new Regex(String.Join("|", swapList.Keys.Select(k => k.Replace("\\", "\\\\"))));
        private static readonly Regex regexUnescapeSwap
            = new Regex(String.Join("|", swapList.Values.Select(v => v.Replace("\\", "\\\\"))));
        #endregion

        #region Initialisation methods
        /// <summary>
        /// Config values MUST NOT be created using this constrctor.
        /// </summary>
        /// <param name="data">---</param>
        public ConfigValue(string data)
            : this(
                 data: data,
                 final: false
            )
        { }

        private ConfigValue(string data, bool final)
        {
            _data = data;
            _values = new Dictionary<string, List<ConfigValue>>();

            if (final)
            {
                _parsedData = (new List<ConfigValue>() { this }).AsReadOnly();
            }
            else
            {
                _parsedData = ParseData(_data).AsReadOnly();
            }
        }


        #region Parsing
        private static List<ConfigValue> ParseData(string data)
        {
            var dataParsed = new List<ConfigValue>();
            if (String.IsNullOrWhiteSpace(data))
            {
                return dataParsed;
            }

            int from = 0;
            int index = 0;
            while (index < data.Length)
            {
                if (data[index] == '"')
                {
                    dataParsed.Add(new ConfigValue(ParseString(data, ref index), true));
                    continue;
                }

                while (index < data.Length && !Char.IsWhiteSpace(data[index]))
                {
                    index += 1;
                }

                dataParsed.Add(new ConfigValue(data.Substring(from, index - from), true));
                while (index < data.Length && Char.IsWhiteSpace(data[index]))
                {
                    index += 1;
                }

                from = index;
            }

            return dataParsed;
        }

        private static string ParseString(string data, ref int index)
        {
            int from = ++index;

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
                throw new ArgumentNullException("name");
            }

            _values.TryGetValue(name.ToUpperInvariant(), out var values);
            return values.AsReadOnly() ?? new List<ConfigValue>().AsReadOnly();
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
            List<ConfigValue> targets = new List<ConfigValue>() { this };

            if (String.IsNullOrEmpty(path))
            {
                return targets;
            }

            var pathLower = path.ToUpperInvariant();
            int position = 0;
            while (position < pathLower.Length)
            {
                if (Char.IsDigit(pathLower[position]))
                {
                    string indexStr = new string(
                        pathLower.Skip(position).TakeWhile(Char.IsDigit).ToArray()
                    );
                    int index = int.Parse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    var newTarget = targets.ElementAtOrDefault(index);
                    if (newTarget == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets.Clear();
                    targets.Add(newTarget);
                    position += indexStr.Length;
                }
                else if (Char.IsLetter(pathLower[position]))
                {
                    string key = new string(
                        pathLower.Skip(position).TakeWhile(Char.IsLetter).ToArray()
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
                else if (pathLower[position] == '$' && Char.IsDigit(pathLower.ElementAtOrDefault(1)))
                {
                    string indexStr = new string(
                        pathLower.Skip(position).TakeWhile(c => Char.IsDigit(c) || c == '$').ToArray()
                    );
                    int index = int.Parse(indexStr.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    var target = targets[0];
                    var newTarget = target.AsConfigArray().ElementAtOrDefault(index);
                    if (newTarget == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets.Clear();
                    targets.Add(newTarget);
                    position += indexStr.Length;
                }
                else
                {
                    throw new FormatException($"Unexpected symbol '{pathLower[0]}'({(byte)pathLower[0]})");
                }
            }

            return targets;
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
            return GetAllByPath(path).FirstOrDefault();
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
        public void Set(string name, ConfigValue value, int index = -1)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            var nameLower = name.ToUpperInvariant();

            if (_values.ContainsKey(nameLower))
            {
                if (index == -1) { _values[nameLower].Add(value); }
                else { _values[nameLower][index] = value; }
            }
            else
            {
                _values[nameLower] = new List<ConfigValue>(new[] { value });
            }
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
                throw new ArgumentNullException("name");
            }

            return _values.ContainsKey(name.ToUpperInvariant());
        }

        /// <summary>
        /// Checks whether any value can be located by path
        /// </summary>
        /// <param name="path">Path to value</param>
        /// <returns>Is config contains any value with given path</returns>
        public bool ContainsPath(string path)
        {
            return GetAllByPath(path).Count > 0;
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
        public string AsEscapedString() => EscapeString(_parsedData[0]._data);

        /// <summary>
        /// Gets data as boolean value
        /// </summary>
        /// <returns>Boolean value</returns>
        public bool AsBoolean() => bool.Parse(_parsedData[0]._data);

        /// <summary>
        /// Gets data as 32 bit integer(int) value
        /// </summary>
        /// <returns>Int32 value</returns>
        public Int32 AsInt() => Int32.Parse(_parsedData[0]._data, NumberStyles.Integer, CultureInfo.InvariantCulture);
        /// <summary>
        /// Gets data as 64 bit integer(long) value
        /// </summary>
        /// <returns>Int64 value</returns>
        public Int64 AsLong() => Int64.Parse(_parsedData[0]._data, NumberStyles.Integer, CultureInfo.InvariantCulture);
        /// <summary>
        /// Gets data as single precision floating point(float) value
        /// </summary>
        /// <returns>Single precision floating point value</returns>
        public float AsFloat() => Single.Parse(_parsedData[0]._data, NumberStyles.Number, CultureInfo.InvariantCulture);
        /// <summary>
        /// Gets data as double precision floating point(double) value
        /// </summary>
        /// <returns>Double precision floating point value</returns>
        public double AsDouble() => Double.Parse(_parsedData[0]._data, NumberStyles.Number, CultureInfo.InvariantCulture);

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
        private static string EscapeString(string data)
            => regexUnescapeSwap.Replace(data, v => swapList.First(x => x.Value == v.Value).Key);

        private static string UnescapeString(string data)
            => regexEscapeSwap.Replace(data, v => swapList[v.Value]);

        private static ConfigValue ConfigValueFromCustom<T>(T value)
        {
            var tc = TypeDescriptor.GetConverter(typeof(string));
            string stringValue = (string)tc.ConvertFrom(value);
            return new ConfigValue(stringValue);
        }
        #endregion
    }
}
