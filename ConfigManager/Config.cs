using System.Collections.Generic;

namespace ConfigManager
{
    public static partial class Config
    {
        private class State
        {
            public int Line { get; set; }
            public string[] Data { get; set; }

            public Stack<ConfigValue> Context { get; private set; }

            public State(string[] data)
            {
                this.Line = 0;
                this.Data = data;
                this.Context = new Stack<ConfigValue>();
            }
        }

        /// <summary>
        /// Creates new Config.
        /// 
        /// Every config value should be written inside core value or enclosed values.
        /// </summary>
        /// <returns>Core config value</returns>
        public static ConfigValue Create() => new ConfigValue(null);
    }
}
