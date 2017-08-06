﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace ConfigManager
{
    public class ConfigValue
    {
        #region Data
        private Dictionary<string, List<ConfigValue>> _values;
        private string _data;
        private List<ConfigValue> _parsedData;

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
            = new Regex(String.Join("|", swapList.Keys.Select(k => k.Replace(@"\", @"\\"))));
        private static readonly Regex regexUnescapeSwap
            = new Regex(String.Join("|", swapList.Values.Select(v => v.Replace(@"\", @"\\"))));
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

        private ConfigValue(string data, bool final = false)
        {
            this._data = data;
            this._values = new Dictionary<string, List<ConfigValue>>();
            this._parsedData = new List<ConfigValue>();

            if (final)
            {
                this._parsedData.Add(this);
            }
            else
            {
                this.ParseData(_data);
            }
        }


        #region Parsing
        private void ParseData(string data)
        {
            if (String.IsNullOrWhiteSpace(data)) return;

            int from = 0;
            int index = 0;
            while (index < data.Length)
            {
                if (data[index] == '"')
                {
                    _parsedData.Add(new ConfigValue(ParseString(data, ref index), true));
                    continue;
                }

                while (index < data.Length && !Char.IsWhiteSpace(data[index]))
                {
                    index += 1;
                }

                _parsedData.Add(new ConfigValue(data.Substring(from, index - from), true));
                while (index < data.Length && Char.IsWhiteSpace(data[index]))
                {
                    index += 1;
                }

                from = index;
            }
        }

        private string ParseString(string data, ref int index)
        {
            int from = ++index;

            while (index < data.Length)
            {
                if (data[index] == '"')
                    return UnescapeString(data.Substring(from, index++ - from));

                if (data[index++] == '\\')
                    ++index;
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
        public List<ConfigValue> GetAll(string name)
        {
            _values.TryGetValue(name.ToLower(), out var values);
            return values ?? new List<ConfigValue>();
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
        public List<ConfigValue> GetAllByPath(string path)
        {
            List<ConfigValue> targets = new List<ConfigValue>() { this };

            if (String.IsNullOrEmpty(path))
            {
                return targets;
            }

            path = path.ToLower();
            while (path != "")
            {
                if (Char.IsDigit(path[0]))
                {
                    string indexStr = new string(path.TakeWhile(Char.IsDigit).ToArray());
                    int index = int.Parse(indexStr);
                    var newTarget = targets.ElementAtOrDefault(index);
                    if (newTarget == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets.Clear();
                    targets.Add(newTarget);
                    path = indexStr.Length == path.Length ? "" : path.Substring(indexStr.Length);
                }
                else if (Char.IsLetter(path[0]))
                {
                    string key = new string(path.TakeWhile(Char.IsLetter).ToArray());
                    var newTargets = targets.FirstOrDefault()?.GetAll(key);
                    if (newTargets == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets = newTargets;
                    path = key.Length == path.Length ? "" : path.Substring(key.Length);
                }
                else if (path[0] == '.')
                {
                    path = path.Substring(1);
                }
                else if (path[0] == '$' && Char.IsDigit(path.ElementAtOrDefault(1)))
                {
                    string indexStr = new string(path.TakeWhile(c => Char.IsDigit(c) || c == '$').ToArray());
                    int index = int.Parse(indexStr.Substring(1));
                    var target = targets[0];
                    var newTarget = target.AsConfigArray().ElementAtOrDefault(index);
                    if (newTarget == null)
                    {
                        return new List<ConfigValue>();
                    }

                    targets.Clear();
                    targets.Add(newTarget);

                    path = indexStr.Length == path.Length ? "" : path.Substring(indexStr.Length);
                }
                else
                {
                    throw new FormatException($"Unexpected symbol '{path[0]}'({(byte)path[0]})");
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
            name = name.ToLower();

            if (_values.ContainsKey(name))
            {
                if (index == -1) _values[name].Add(value);
                else _values[name][index] = value;
            }
            else
            {
                _values[name] = new List<ConfigValue>(new[] { value });
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
            return _values.ContainsKey(name.ToLower());
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
        public string AsString() => _data;
        /// <summary>
        /// Gets data as escaped raw string
        /// </summary>
        /// <returns>Escaped string data</returns>
        public string AsEscapedString() => EscapeString(_data);

        /// <summary>
        /// Gets data as boolean value
        /// </summary>
        /// <returns>Boolean value</returns>
        public bool AsBoolean() => bool.Parse(_data);

        /// <summary>
        /// Gets data as 32 bit integer(int) value
        /// </summary>
        /// <returns>Int32 value</returns>
        public Int32 AsInt() => Int32.Parse(_data);
        /// <summary>
        /// Gets data as 64 bit integer(long) value
        /// </summary>
        /// <returns>Int64 value</returns>
        public Int64 AsLong() => Int64.Parse(_data);
        /// <summary>
        /// Gets data as single precision floating point(float) value
        /// </summary>
        /// <returns>Single precision floating point value</returns>
        public float AsFloat() => Single.Parse(_data);
        /// <summary>
        /// Gets data as double precision floating point(double) value
        /// </summary>
        /// <returns>Double precision floating point value</returns>
        public double AsDouble() => Double.Parse(_data);

        /// <summary>
        /// Gets data as a list of ConfigValues.
        /// data is splitted by whitespace.
        /// </summary>
        /// <returns>Data as a list of Config values</returns>
        public List<ConfigValue> AsConfigList() => _parsedData;
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
        public List<string> AsList() => _parsedData.Select(cv => cv.AsString()).ToList();
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