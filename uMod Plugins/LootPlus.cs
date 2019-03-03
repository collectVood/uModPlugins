using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("LootPlus", "Iv Misticos", "1.0.2")]
    [Description("Modify loot on your server.")]
    class LootPlus : RustPlugin
    {
        #region Variables

        private static LootPlus _ins;
        
        private Random _random = new Random();
        
        #endregion
        
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Loot Skins", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, Dictionary<string, ulong>> Skins = null; // OLD
            
            [JsonProperty(PropertyName = "Containers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ContainerData> Containers = new List<ContainerData> {new ContainerData()};
        }

        private class ContainerData
        {
            [JsonProperty(PropertyName = "Entity Shortname")]
            public string Shortname = "entity.shortname";

            [JsonProperty(PropertyName = "Add (true) / Refresh (false)")]
            public bool AddItems = false;
            
            [JsonProperty(PropertyName = "Items Count", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AmountData> Count = new List<AmountData> {new AmountData()};
            
            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemData> Items = new List<ItemData> {new ItemData()};
        }

        private class AmountData
        {
            [JsonProperty(PropertyName = "Amount")]
            public int Amount = 3;
            
            [JsonProperty(PropertyName = "Chance")]
            public int Chance = 10;

            public static int Select(List<AmountData> data)
            {
                var sum1 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum1 += entry.Chance;
                }

                var random = _ins._random.Next(0, sum1);
                var sum2 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum2 += entry.Chance;
                    if (random <= sum2)
                        return entry.Amount;
                }
                
                return -1;
            }
        }

        private class ItemData
        {
            [JsonProperty(PropertyName = "Item Shortname")]
            public string Shortname = "item.shortname";
            
            [JsonProperty(PropertyName = "Count")]
            public List<AmountData> Count = new List<AmountData> {new AmountData()};
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            LoadConfig();

            NextFrame(() =>
            {
                var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
                var containersCount = containers.Length;
                for (var i = 0; i < containersCount; i++)
                {
                    var container = containers[i];
                    LootHandler(container);
                }
            });
        }

        private void OnLootSpawn(StorageContainer container) => NextFrame(() => LootHandler(container));
        
        #endregion
        
        #region Controller
        
        
        
        #endregion
        
        #region Helpers
        
        private void LootHandler(StorageContainer entity)
        {
            if (entity == null)
                return;

            for (var i = 0; i < _config.Containers.Count; i++)
            {
                var container = _config.Containers[i];
                if (container.Shortname != entity.ShortPrefabName)
                    continue;

                for (int j = 0; j < container.Items.Count; j++)
                {
                    
                }
            }
        }
        
        #endregion
    }
}