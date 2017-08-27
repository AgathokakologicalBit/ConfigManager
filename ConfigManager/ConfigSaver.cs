using System;
using System.IO;
using System.Text;

namespace ConfigManager
{
    public static partial class Config
    {
        public static readonly string BaseIndentation = "  ";

        public static string SaveToString(ConfigValue config)
        {
            return GenerateString(config, String.Empty);
        }

        public static void SaveToStream(ConfigValue config, TextWriter writer)
        {
            writer.Write(GenerateString(config, ""));
        }

        private static string GenerateString(ConfigValue config, string currentIndentation)
        {
            string[] keys = config.GetKeys();
            if (keys.Length == 0)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder(keys.Length * 2);
            foreach (string key in keys)
            {
                var valuesList = config.GetAll(key);

                foreach (var value in valuesList) {
                    var raw = value.AsRawString();
                    if (!String.IsNullOrEmpty(raw)) { raw = " " + raw; }

                    builder.AppendLine($"{currentIndentation}{key}{raw}");
                    builder.Append(GenerateString(value, currentIndentation + BaseIndentation));
                }
            }

            return builder.ToString();
        }
    }
}
