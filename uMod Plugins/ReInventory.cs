using System;
using System.Collections.Generic;
using System.Drawing;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ReInventory", "Iv Misticos", "1.0.0")]
    [Description("In-game inventory API system")]
    class ReInventory : RustPlugin
    {
        #region Variables

        private static PluginData _data;
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Interface")]
            public InterfaceConfig Interface = new InterfaceConfig();
            
            [JsonProperty(PropertyName = "Inventory Size", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<InventorySize> InventorySizes = new List<InventorySize> {new InventorySize()};
            
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }

        private class InterfaceConfig
        {
            [JsonProperty(PropertyName = "Background")]
            public InterfaceBackgroundConfig Background = new InterfaceBackgroundConfig();
            
            [JsonProperty(PropertyName = "Main")]
            public InterfaceMainConfig Main = new InterfaceMainConfig();
            
            [JsonProperty(PropertyName = "Headline")]
            public InterfaceHeadlineConfig Headline = new InterfaceHeadlineConfig();
            
            [JsonProperty(PropertyName = "Items Container")]
            public InterfaceItemsContainerConfig Container = new InterfaceItemsContainerConfig();
            
            [JsonProperty(PropertyName = "Items")]
            public InterfaceItemsConfig Items = new InterfaceItemsConfig();
        }

        private class InterfaceBackgroundConfig
        {
            [JsonProperty(PropertyName = "Width")]
            public int Width = 800;
            
            [JsonProperty(PropertyName = "Height")]
            public int Height = 400;
            
            [JsonProperty(PropertyName = "X Offset")]
            public int OffsetX = 0;
            
            [JsonProperty(PropertyName = "Y Offset")]
            public int OffsetY = 0;

            [JsonProperty(PropertyName = "Color")]
            public string Color = "#424242";
        }

        private class InterfaceMainConfig
        {
            
        }

        private class InterfaceHeadlineConfig
        {
            
        }

        private class InterfaceItemsContainerConfig
        {
            
        }

        private class InterfaceItemsConfig
        {
            
        }

        private class InventorySize
        {
            public string Permission = string.Empty;

            public int Size = 6;
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
            public List<PlayerData> Players = new List<PlayerData>();
        }

        private class PlayerData
        {
            public ulong Id;
            
            // TODO: Last join/disconnect date

            public List<ItemData> Items = new List<ItemData>();

            public static PlayerData Find(ulong id)
            {
                for (var i = 0; i < _data.Players.Count; i++)
                {
                    if (_data.Players[i].Id == id)
                        return _data.Players[i];
                }

                return null;
            }

            public static void Initialize(ulong id)
            {
                if (Find(id) != null)
                    return;
                
                _data.Players.Add(new PlayerData
                {
                    Id = id
                });
            }
        }

        private class ItemData
        {
            public string IconUrl;
            public string Command;
            public object[] Arguments;
        }

        #endregion
        
        #region Hooks

        private void Loaded()
        {
            LoadData();
            
            // TODO: Purge
            
            for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                OnPlayerInit(BasePlayer.activePlayerList[i]);
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            PlayerData.Initialize(player.userID);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            // TODO
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            // TODO: disconnected hook
            
            SaveData();
        }
        
        #endregion
        
        #region Commands
        
        // TODO: Config universal UI command
        
        #endregion
        
        #region Helpers

        private void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);
        
        private static string GetColor(string hex, float alpha)
        {
            var color = ColorTranslator.FromHtml(hex);
            var r = Convert.ToInt16(color.R) / 255f;
            var g = Convert.ToInt16(color.G) / 255f;
            var b = Convert.ToInt16(color.B) / 255f;

            return $"{r} {g} {b} {alpha}";
        }

        #endregion
    }
}