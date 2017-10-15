using System.IO;
using System.Text;

namespace ConfigManager
{
    public static partial class Config
    {
        public const string BaseIndentation = "  ";

        public static string SaveToString(ConfigValue config)
        {
            return GenerateString(config, string.Empty);
        }

        public static void SaveToStream(ConfigValue config, TextWriter writer)
        {
            writer.Write(GenerateString(config, ""));
        }

        private static string GenerateString(ConfigValue config, string currentIndentation)
        {
            var keys = config.GetKeys();
            if (keys.Length == 0)
            {
                return "";
            }

            var builder = new StringBuilder(keys.Length * 2);
            foreach (var key in keys)
            {
                var valuesList = config.GetAll(key);

                foreach (var value in valuesList) {
                    var raw = value.AsRawString();
                    if (!string.IsNullOrEmpty(raw)) { raw = " " + raw; }

                    builder.AppendLine($"{currentIndentation}{key}{raw}");
                    builder.Append(GenerateString(value, currentIndentation + BaseIndentation));
                }
            }

            return builder.ToString();
        }
    }
}
