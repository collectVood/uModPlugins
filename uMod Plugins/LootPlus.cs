using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Loot Plus", "Iv Misticos", "2.0.3")]
    [Description("Modify loot on your server.")]
    public class LootPlus : RustPlugin
    {
        #region Variables

        public static LootPlus Ins;

        public Random Random = new Random();

        private bool _initialized = false;

        private const string PermissionLootSave = "lootplus.lootsave";
        
        private const string PermissionLootRefill = "lootplus.lootrefill";
        
        #endregion
        
        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Plugin Enabled")]
            public bool Enabled = false;
            
            [JsonProperty(PropertyName = "Container Loot Save Command")]
            public string LootSaveCommand = "lootsave";
            
            [JsonProperty(PropertyName = "Container Refill Command")]
            public string LootRefillCommand = "lootrefill";
            
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

        private class ContainerData
        {
            [JsonProperty(PropertyName = "Entity Shortname")]
            public string Shortname = "entity.shortname";

            [JsonProperty(PropertyName = "Replace Items")]
            public bool ReplaceItems = true;

            [JsonProperty(PropertyName = "Add Items")]
            public bool AddItems = false;

            [JsonProperty(PropertyName = "Modify Items")]
            public bool ModifyItems = false;

            [JsonProperty(PropertyName = "Maximal Failures To Add An Item")]
            public int MaxRetries = 5;
            
            [JsonProperty(PropertyName = "Capacity", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CapacityData> Capacity = new List<CapacityData> {new CapacityData()};
            
            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemData> Items = new List<ItemData> {new ItemData()};
        }

        private class ItemData : ChanceData
        {
            [JsonProperty(PropertyName = "Item Shortname")]
            public string Shortname = "item.shortname";

            [JsonProperty(PropertyName = "Item Name (Empty To Ignore)")]
            public string Name = "";

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

        private class ConditionData : ChanceData
        {
            [JsonProperty(PropertyName = "Condition")]
            public float Condition = 100f;
        }

        private class SkinData : ChanceData
        {
            [JsonProperty(PropertyName = "Skin")]
            // ReSharper disable once RedundantDefaultMemberInitializer
            public ulong Skin = 0;
        }

        private class AmountData : ChanceData
        {
            [JsonProperty(PropertyName = "Amount")]
            public int Amount = 3;
            
            [JsonProperty(PropertyName = "Rate")]
            public float Rate = -1f;
        }

        private class CapacityData : ChanceData
        {
            [JsonProperty(PropertyName = "Capacity")]
            public int Capacity = 3;
        }

        public class ChanceData
        {
            [JsonProperty(PropertyName = "Chance")]
            // ReSharper disable once MemberCanBePrivate.Global
            public int Chance = 1;
            
            public static T Select<T>(IReadOnlyList<T> data) where T : ChanceData
            {
                // xD

                if (data == null)
                {
                    PrintDebug("Data is null");
                    return null;
                }

                if (data.Count == 0)
                {
                    PrintDebug("Data is empty");
                    return null;
                }

                var sum1 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum1 += entry?.Chance ?? 0;
                }

                PrintDebug($"Sum: {sum1}");
                if (sum1 < 1)
                {
                    PrintDebug("Sum is less than 1");
                    return null;
                }

                var random = Ins?.Random?.Next(1, sum1 + 1); // include the sum1 number itself and exclude the 0
                if (random == null)
                {
                    PrintDebug("Random is null");
                    return null;
                }
                
                PrintDebug($"Selected random: {random}");
                
                var sum2 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum2 += entry?.Chance ?? 0;
                    PrintDebug($"Current sum: {sum2}, random: {random}");
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
        
        #region Commands

        private void CommandLootSave(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null)
            {
                iplayer.Reply(GetMsg("In-Game Only", iplayer.Id));
                return;
            }

            if (!iplayer.HasPermission(PermissionLootSave))
            {
                iplayer.Reply(GetMsg("No Permission", iplayer.Id));
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 10f) || hit.GetEntity() == null ||
                !(hit.GetEntity() is LootContainer))
            {
                iplayer.Reply(GetMsg("No Loot Container", iplayer.Id));
                return;
            }

            var container = hit.GetEntity() as LootContainer;
            if (container == null || container.inventory == null)
            {
                // Shouldn't really happen
                return;
            }

            var inventory = player.inventory.containerMain;

            var containerData = new ContainerData
            {
                ModifyItems = false,
                AddItems = false,
                ReplaceItems = true,
                Shortname = container.ShortPrefabName,
                Capacity = new List<CapacityData>
                {
                    new CapacityData
                    {
                        Capacity = inventory.itemList.Count
                    }
                },
                Items = new List<ItemData>()
            };

            foreach (var item in inventory.itemList)
            {
                var isBlueprint = item.IsBlueprint();
                var itemData = new ItemData
                {
                    Amount = new List<AmountData>
                    {
                        new AmountData
                        {
                            Amount = item.amount
                        }
                    },
                    Conditions = new List<ConditionData>(),
                    Skins = new List<SkinData>
                    {
                        new SkinData
                        {
                            Skin = item.skin
                        }
                    },
                    Shortname = isBlueprint ? item.blueprintTargetDef.shortname : item.info.shortname,
                    Name = item.name,
                    AllowStacking = true,
                    IsBlueprint = isBlueprint
                };

                if (!isBlueprint)
                {
                    if (item.hasCondition)
                        itemData.Conditions.Add(new ConditionData
                        {
                            Condition = item.condition
                        });
                }

                containerData.Items.Add(itemData);
            }

            _config.Containers.Add(containerData);
            SaveConfig();
            iplayer.Reply(GetMsg("Loot Container Saved", iplayer.Id));
        }

        private void CommandLootRefill(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionLootRefill))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }
            
            player.Reply(GetMsg("Loot Refill Started", player.Id));
            LootRefill();
        }

        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"In-Game Only", "Please, use this only while you're in the game"},
                {"No Permission", "You don't have enough permissions"},
                {"No Loot Container", "Please, look at the loot container in 10m"},
                {"Loot Container Saved", "You have saved this loot container data to configuration"},
                {"Loot Refill Started", "Loot refill process just started"}
            }, this);
        }

        private void OnServerInitialized()
        {
            Ins = this;
            
            permission.RegisterPermission(PermissionLootSave, this);
            permission.RegisterPermission(PermissionLootRefill, this);

            // Converting old configuration
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

            if (!_config.Enabled)
            {
                PrintWarning("WARNING! Plugin is disabled in configuration");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            AddCovalenceCommand(_config.LootSaveCommand, nameof(CommandLootSave));
            AddCovalenceCommand(_config.LootRefillCommand, nameof(CommandLootRefill));

            _initialized = true;

            NextFrame(LootRefill);
        }

        private void Unload()
        {
            LootPlusController.TryDestroy();
            _initialized = false;
            
            // LOOT IS BACK
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            var containersCount = containers.Length;
            for (var i = 0; i < containersCount; i++)
            {
                var container = containers[i];
                
                // Creating an inventory
                container.CreateInventory(true);
                
                // Spawning loot
                container.SpawnLoot();
                
                // Changing the capacity
                container.inventory.capacity = container.inventory.itemList.Count;
            }
        }

        private void OnLootSpawn(StorageContainer container)
        {
            if (!_initialized)
                return;
            
            NextFrame(() => LootPlusController.Instance.StartCoroutine(LootHandler(container)));
        }

        #endregion
        
        #region Controller
        
        private class LootPlusController : FacepunchBehaviour
        {
            private static LootPlusController _instance;

            public static LootPlusController Instance => _instance ? _instance : new GameObject().AddComponent<LootPlusController>();

            private void Awake()
            {
                TryDestroy();
                _instance = this;
            }

            public static void TryDestroy()
            {
                if (_instance)
                    Destroy(_instance.gameObject);
            }
        }
        
        #endregion
        
        #region Helpers

        private IEnumerator LootHandler(StorageContainer entity)
        {
            if (entity == null)
                yield break;

            for (var i = 0; i < _config.Containers.Count; i++)
            {
                var container = _config.Containers[i];
                if (container.Shortname != "global" && container.Shortname != entity.ShortPrefabName)
                    continue;

                HandleContainer(entity, container);
            }
        }

        private void HandleContainer(StorageContainer entity, ContainerData container)
        {
            PrintDebug(
                $"Handling container {entity.ShortPrefabName} ({entity.net.ID} @ {entity.transform.position})");

            if (_config.ShuffleItems && !container.ModifyItems) // No need to shuffle for items modification
                container.Items?.Shuffle();

            entity.inventory.capacity = entity.inventory.itemList.Count;
            HandleInventory(entity.inventory, container);
        }

        private void HandleInventory(ItemContainer inventory, ContainerData container)
        {
            var dataCapacity = ChanceData.Select(container.Capacity);
            if (dataCapacity == null)
            {
                PrintDebug("Could not select a correct capacity");
                return;
            }
            
            PrintDebug($"Items: {inventory.itemList.Count} / {inventory.capacity}");

            if (!((container.AddItems || container.ReplaceItems) ^ container.ModifyItems))
            {
                PrintWarning("Multiple options (Add / Replace / Modify) are selected");
                return;
            }

            if (container.ReplaceItems)
            {
                inventory.Clear();
                ItemManager.DoRemoves();
                inventory.capacity = dataCapacity.Capacity;
                HandleInventoryAddReplace(inventory, container);
                return;
            }
            
            if (container.AddItems)
            {
                inventory.capacity += dataCapacity.Capacity;
                HandleInventoryAddReplace(inventory, container);
                return;
            }

            if (container.ModifyItems)
            {
                HandleInventoryModify(inventory, container);
            }
        }

        private static void HandleInventoryAddReplace(ItemContainer inventory, ContainerData container)
        {
            PrintDebug("Using add or replace");
            
            var failures = 0;
            while (inventory.itemList.Count < inventory.capacity)
            {
                PrintDebug($"Count: {inventory.itemList.Count} / {inventory.capacity}");

                var dataItem = ChanceData.Select(container.Items);
                if (dataItem == null)
                {
                    PrintDebug("Could not select a correct item");
                    continue;
                }

                PrintDebug($"Handling item {dataItem.Shortname} (Blueprint: {dataItem.IsBlueprint} / Stacking: {dataItem.AllowStacking})");

                var skin = ChanceData.Select(dataItem.Skins)?.Skin ?? 0UL;

                if (!_config.DuplicateItems) // Duplicate items are not allowed
                {
                    PrintDebug("Searching for duplicates..");

                    if (IsDuplicate(inventory.itemList, dataItem, skin))
                    {
                        if (++failures > container.MaxRetries)
                        {
                            PrintDebug("Too many failures");
                            break;
                        }

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

                var amount = 1;
                if (dataAmount.Amount > 0)
                    amount = dataAmount.Amount;

                if (dataAmount.Rate > 0f)
                    amount = (int) (dataAmount.Rate * amount);
                
                PrintDebug($"Selected amount: {amount} (Amount: {dataAmount.Amount} / Rate: {dataAmount.Rate})");

                var definition =
                    ItemManager.FindItemDefinition(dataItem.IsBlueprint ? "blueprintbase" : dataItem.Shortname);
                if (definition == null)
                {
                    PrintDebug("Could not find an item definition");
                    continue;
                }

                var createdItem = ItemManager.Create(definition, amount, skin);
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

                if (!string.IsNullOrEmpty(dataItem.Name))
                    createdItem.name = dataItem.Name;

                PrintDebug("Moving item to container..");

                var moved = createdItem.MoveToContainer(inventory, allowStack: dataItem.AllowStacking);
                if (moved) continue;

                PrintDebug("Could not move item to a container");
            }
        }

        private static void HandleInventoryModify(ItemContainer inventory, ContainerData container)
        {
            PrintDebug("Using modify");
            
            for (var i = 0; i < inventory.itemList.Count; i++)
            {
                var item = inventory.itemList[i];
                for (var j = 0; j < container.Items.Count; j++)
                {
                    var dataItem = container.Items[j];
                    if (dataItem.Shortname != "global" && dataItem.Shortname != item.info.shortname ||
                        dataItem.IsBlueprint != item.IsBlueprint())
                        continue;

                    PrintDebug(
                        $"Handling item {dataItem.Shortname} (Blueprint: {dataItem.IsBlueprint} / Stacking: {dataItem.AllowStacking})");

                    var skin = ChanceData.Select(dataItem.Skins)?.Skin;
                    if (skin.HasValue)
                        item.skin = skin.Value;

                    var dataAmount = ChanceData.Select(dataItem.Amount);
                    if (dataAmount == null)
                    {
                        PrintDebug("Could not select a correct amount");
                        continue;
                    }

                    var amount = item.amount;
                    if (dataAmount.Amount > 0)
                        amount = dataAmount.Amount;

                    if (dataAmount.Rate > 0f)
                        amount = (int) (dataAmount.Rate * amount);
                    
                    PrintDebug($"Selected amount: {amount} (Amount: {dataAmount.Amount} / Rate: {dataAmount.Rate})");

                    item.amount = amount;
                    
                    PrintDebug("Setting up condition..");

                    var dataCondition = ChanceData.Select(dataItem.Conditions);
                    if (item.hasCondition)
                    {
                        if (dataCondition == null)
                        {
                            PrintDebug("Could not select a correct condition");
                        }
                        else
                        {
                            PrintDebug($"Selected condition: {dataCondition.Condition}");
                            item.condition = dataCondition.Condition;
                        }
                    }
                    else if (dataCondition != null)
                    {
                        PrintDebug("Configurated item has a condition but item doesn't have condition");
                    }

                    if (!string.IsNullOrEmpty(dataItem.Name))
                        item.name = dataItem.Name;
                }
            }
        }

        private static bool IsDuplicate(IReadOnlyList<Item> list, ItemData dataItem, ulong skin)
        {
            for (var j = 0; j < list.Count; j++)
            {
                var item = list[j];
                if (dataItem.IsBlueprint)
                {
                    if (!item.IsBlueprint() || item.blueprintTargetDef.shortname != dataItem.Shortname) continue;

                    PrintDebug("Found a duplicate blueprint");
                    return true;
                }

                if (item.info.shortname != dataItem.Shortname) continue;
                if (_config.DuplicateItemsDifferentSkins && item.skin != skin)
                    continue;

                PrintDebug("Found a duplicate");
                return true;
            }

            return false;
        }

        private string GetMsg(string key, string userId) => lang.GetMessage(key, this, userId);

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }

        private void LootRefill()
        {
            var containers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            var containersCount = containers.Length;
            for (var i = 0; i < containersCount; i++)
            {
                var container = containers[i];
                LootPlusController.Instance.StartCoroutine(LootHandler(container));
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