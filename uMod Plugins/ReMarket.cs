using System;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ReMarket", "Iv Misticos", "1.0.0")]
    [Description("In-game market system")]
    class ReMarket : RustPlugin
    {
        #region Variables

        private PluginData _data;
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);
        
        #endregion
        
        #region Work with Data

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            // TODO
        }

        #endregion
        
        #region Hooks
        
        
        
        #endregion
        
        #region Commands


        
        #endregion
        
        #region Helpers

        private void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}