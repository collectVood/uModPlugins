using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = System.Random;
//using Oxide.Extensions;

namespace Oxide.Plugins
{
    [Info("Loot Plus", "Iv Misticos", "2.0.0")]
    [Description("Modify loot on your server.")]
    public class LootPlus : RustPlugin
    {
        #region Variables

        public static LootPlus Ins;

        public Random Random = new Random();
        
        #endregion
        
        #region Configuration

        public static Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Plugin Enabled")]
            public bool Enabled = false;
            
            [JsonProperty(PropertyName = "Loot Skins", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, Dictionary<string, ulong>> Skins = null; // OLD
            
            [JsonProperty(PropertyName = "Containers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ContainerData> Containers = new List<ContainerData> {new ContainerData()};

            [JsonProperty(PropertyName = "Shuffle Items")]
            public bool ShuffleItems = true;

            [JsonProperty(PropertyName = "Allow Duplicate Items")]
            public bool DuplicateItems = false;

            [JsonProperty(PropertyName = "Allow Duplicate Items With Different Skins")]
            public bool DuplicateItemsDifferentSkins = true;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }

        public class ContainerData
        {
            [JsonProperty(PropertyName = "Entity Shortname")]
            public string Shortname = "entity.shortname";

            [JsonProperty(PropertyName = "Replace (true) / Add (false)")]
            public bool ReplaceItems = false;
            
            [JsonProperty(PropertyName = "Capacity", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CapacityData> Capacity = new List<CapacityData> {new CapacityData()};
            
            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemData> Items = new List<ItemData> {new ItemData()};
        }

        public class ItemData : ChanceData
        {
            [JsonProperty(PropertyName = "Item Shortname")]
            public string Shortname = "item.shortname";

            [JsonProperty(PropertyName = "Is Blueprint")]
            public bool IsBlueprint = false;

            [JsonProperty(PropertyName = "Allow Stacking")]
            public bool AllowStacking = true;

            [JsonProperty(PropertyName = "Conditions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConditionData> Conditions = new List<ConditionData> {new ConditionData()};

            [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SkinData> Skins = new List<SkinData> {new SkinData()};
            
            [JsonProperty(PropertyName = "Amount", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AmountData> Amount = new List<AmountData> {new AmountData()};
        }
        
        #region Additional

        public class ConditionData : ChanceData
        {
            [JsonProperty(PropertyName = "Condition")]
            public float Condition = 100f;
        }

        public class SkinData : ChanceData
        {
            [JsonProperty(PropertyName = "Skin")]
            // ReSharper disable once RedundantDefaultMemberInitializer
            public ulong Skin = 0;
        }

        public class AmountData : ChanceData
        {
            [JsonProperty(PropertyName = "Amount")]
            public int Amount = 3;
        }

        public class CapacityData : ChanceData
        {
            [JsonProperty(PropertyName = "Capacity")]
            public int Capacity = 3;
        }

        public class ChanceData
        {
            [JsonProperty(PropertyName = "Chance")]
            // ReSharper disable once MemberCanBePrivate.Local
            public int Chance = 1;
            
            public static T Select<T>(IReadOnlyList<T> data) where T : ChanceData
            {
                // xD

                if (data == null)
                {
                    PrintDebug("Data is null");
                    return null;
                }

                var sum1 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum1 += entry?.Chance ?? 0;
                }

                var random = Ins?.Random?.Next(0, sum1);
                if (random == null)
                {
                    PrintDebug("Random is null");
                    return null;
                }
                
                var sum2 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum2 += entry?.Chance ?? 0;
                    if (random <= sum2)
                        return entry;
                }
                
                return null;
            }
        }
        
        #endregion

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
        
        #region Hooks

        private void OnServerInitialized()
        {
            Ins = this;

            if (_config.Skins != null)
            {
                foreach (var kvp in _config.Skins)
                {
                    var container = kvp.Key;
                    var dataContainer = new ContainerData
                    {
                        Shortname = container,
                        Items = new List<ItemData>()
                    };
                    
                    foreach (var item in kvp.Value)
                    {
                        var shortname = item.Key;
                        var skin = item.Value;

                        var dataItem = new ItemData
                        {
                            Shortname = shortname,
                            Skins = new List<SkinData>
                            {
                                new SkinData
                                {
                                    Skin = skin
                                }
                            }
                        };
                        
                        dataContainer.Items.Add(dataItem);
                    }
                    
                    _config.Containers.Add(dataContainer);
                }
            }

            new GameObject().AddComponent<LootPlusController>();

            if (!_config.Enabled)
            {
                PrintWarning("WARNING! Plugin is disabled in configuration");
                return;
            }

            NextFrame(() =>
            {
                var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
                var containersCount = containers.Length;
                for (var i = 0; i < containersCount; i++)
                {
                    var container = containers[i];
//                    LootHandler(container, false);
                    LootPlusController.Instance.RunCoroutine(LootHandler(container));
                }
            });
        }

        private void Unload()
        {
            UnityEngine.Object.Destroy(LootPlusController.Instance);
        }

        private void OnLootSpawn(StorageContainer container)
        {
            if (!_config.Enabled)
                return;
            
//            NextFrame(() => LootHandler(container, true));
            NextFrame(() => LootPlusController.Instance.RunCoroutine(LootHandler(container)));
        }

        #endregion
        
        #region Controller
        
        
        
        #endregion
        
        #region Helpers

        private IEnumerator LootHandler(StorageContainer entity)
        {
            if (entity == null)
                yield break;

            for (var i = 0; i < _config.Containers.Count; i++)
            {
                var container = _config.Containers[i];
                if (container.Shortname != entity.ShortPrefabName)
                    continue;

                PrintDebug(
                    $"Handling container {entity.ShortPrefabName} ({entity.net.ID} @ {entity.transform.position})");

                var dataCapacity = ChanceData.Select(container.Capacity);
                if (dataCapacity == null)
                {
                    PrintDebug("Could not select a correct capacity");
                    continue;
                }

                if (container.ReplaceItems)
                {
                    entity.inventory.Clear();
                    ItemManager.DoRemoves();
                    entity.inventory.capacity = dataCapacity.Capacity;
                }
                else
                {
                    entity.inventory.capacity += dataCapacity.Capacity;
                }

                if (_config.ShuffleItems)
                    container.Items?.Shuffle();
                
                PrintDebug($"Filling up with items.. ({entity.inventory.itemList.Count} / {entity.inventory.capacity})");

                while (entity.inventory.itemList.Count < entity.inventory.capacity)
                {
                    PrintDebug($"Count: {entity.inventory.itemList.Count} / {entity.inventory.capacity}");

                    var dataItem = ChanceData.Select(container.Items);
                    if (dataItem == null)
                    {
                        PrintDebug("Could not select a correct item");
                        continue;
                    }

                    PrintDebug($"Handling item {dataItem.Shortname}");

                    var skin = ChanceData.Select(dataItem.Skins)?.Skin ?? 0UL;

                    if (!_config.DuplicateItems) // Duplicate items are not allowed
                    {
                        PrintDebug("Searching for duplicates..");

                        if (IsDuplicate(entity.inventory.itemList, dataItem, skin))
                        {
                            continue;
                        }
                        
                        PrintDebug("No duplicates");
                    }

                    var dataAmount = ChanceData.Select(dataItem.Amount);
                    if (dataAmount == null)
                    {
                        PrintDebug("Could not select a correct amount");
                        continue;
                    }
                    
                    PrintDebug($"Selected amount: {dataAmount.Amount}");

                    var definition =
                        ItemManager.FindItemDefinition(dataItem.IsBlueprint ? "blueprintbase" : dataItem.Shortname);
                    if (definition == null)
                    {
                        PrintDebug("Could not find an item definition");
                        continue;
                    }

                    var createdItem = ItemManager.Create(definition, dataAmount.Amount, skin);
                    if (createdItem == null)
                    {
                        PrintDebug("Could not create an item");
                        continue;
                    }

                    if (dataItem.IsBlueprint)
                    {
                        createdItem.blueprintTarget = ItemManager.FindItemDefinition(dataItem.Shortname).itemid;
                    }
                    else
                    {
                        PrintDebug("Setting up condition..");
                        
                        var dataCondition = ChanceData.Select(dataItem.Conditions);
                        if (createdItem.hasCondition)
                        {
                            if (dataCondition == null)
                            {
                                PrintDebug("Could not select a correct condition");
                            }
                            else
                            {
                                PrintDebug($"Selected condition: {dataCondition.Condition}");
                                createdItem.condition = dataCondition.Condition;
                            }
                        }
                        else if (dataCondition != null)
                        {
                            PrintDebug("Configurated item has a condition but item doesn't have condition");
                        }
                    }

                    PrintDebug("Moving item to container..");
                    
                    var moved = createdItem.MoveToContainer(entity.inventory, allowStack: dataItem.AllowStacking);
                    if (moved) continue;

                    PrintDebug("Could not move item to a container");
                }
            }
        }

        public static bool IsDuplicate(List<Item> list, ItemData dataItem, ulong skin)
        {
            for (var j = 0; j < list.Count; j++)
            {
                // This could be a huge one line
                // But it'd be very complicated then, so no.
                            
                var item = list[j];
                if (item.IsBlueprint() && dataItem.IsBlueprint &&
                    item.blueprintTargetDef.shortname == dataItem.Shortname)
                {
                    PrintDebug("Found a duplicate blueprint");
                    return true;
                }

                // ReSharper disable once InvertIf
                if (item.info.shortname == dataItem.Shortname)
                {
                    if (_config.DuplicateItemsDifferentSkins && item.skin != skin)
                        continue;

                    PrintDebug("Found a duplicate");
                    return true;
                }
            }

            return false;
        }

//        public bool CanBeFilled(IEnumerable<Item> list, List<ItemData> items, int capacity)
//        {
//            var newList = new List<Item>(list);
//            for (var i = 0; i < items.Count; i++)
//            {
//                var item = items[i];
//                for (var j = 0; j < item.Amount.Count; j++)
//                {
//                    var amount = item.Amount[j].Amount;
//                    
//                    if (_config.DuplicateItemsDifferentSkins)
//                    {
//                        for (var k = 0; k < item.Skins.Count; k++)
//                        {
//                            var skin = item.Skins[k];
//                            if (!IsDuplicate(newList, item, skin.Skin))
//                                newList.Add(ItemManager.CreateByName(item.Shortname, amount, skin.Skin));
//                        }
//                    }
//                    else
//                    {
//                        if (!IsDuplicate(newList, item, 0))
//                            newList.Add(ItemManager.CreateByName(item.Shortname, amount));
//                    }
//                }
//            }
//            
//            return 
//        }

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }
        
        private class LootPlusController : FacepunchBehaviour
        {
            public static LootPlusController Instance;

            private void Awake()
            {
                if (Instance != null)
                    Destroy(Instance.gameObject);
                
                Instance = this;
            }

            public void RunCoroutine(IEnumerator coroutine)
            {
                StartCoroutine(coroutine);
            }
        }
        
        #endregion
    }
    
    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var count = list.Count;
            while (count > 1)
            {
                count--;
                var index = LootPlus.Ins.Random.Next(count + 1);
                var value = list[index];
                list[index] = list[count];
                list[count] = value;
            }
        }
    }
}

//namespace Oxide.Extensions
//{
//}