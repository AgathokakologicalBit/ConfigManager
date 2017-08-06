namespace ConfigManager
{
    public static partial class Config
    {
        /// <summary>
        /// Creates new Config.
        /// 
        /// Every config value should be written inside core value or enclosed values.
        /// </summary>
        /// <returns>Core config value</returns>
        public static ConfigValue Create() => new ConfigValue(null);
    }
}
