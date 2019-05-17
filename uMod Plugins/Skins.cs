using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;

namespace Oxide.Plugins
{
    [Info("Skins", "Iv Misticos", "2.0.0")]
    [Description("Allow players to change items skin with the skin from steam workshop.")]
    class Skins : RustPlugin
    {
        #region Variables
        
        private const string BoxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private static List<ContainerController> _boxes = new List<ContainerController>();

        private const string PermissionUse = "skins.use";
        private const string PermissionAdmin = "skins.admin";
        
        #endregion

        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Command")]
            public string Command = "skin";
            
            [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<ulong>> Skins = new Dictionary<string, List<ulong>>
            {
                { "shortname", new List<ulong>
                {
                    0
                } }
            };
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
                Config.WriteObject(_config, false, $"{Interface.GetMod().ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        
        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Not Allowed", "You don't have permission to use this command" },
                { "Cannot Use", "I'm sorry, you cannot use that right now" },
                { "Help", "Command usage:\n" +
                          "skin show - Show skins\n" +
                          "skin get - Get Skin ID of the item" },
                { "Admin Help", "Admin command usage:\n" +
                          "skin show - Show skins\n" +
                          "skin get - Get Skin ID of the item\n" +
                          "skin remove (Shortname) (Skin ID) - Remove a skin\n" +
                          "skin add (Shortname) (Skin ID) - Add a skin" },
                { "Skin Get Format", "{shortname}'s skin: {id}" },
                { "Skin Get No Item", "Please, hold the needed item" },
                { "Incorrect Skin", "You have entered an incorrect skin" },
                { "Skin Already Exists", "This skin already exists on this item" },
                { "Skin Does Not Exist", "This skin does not exist" },
                { "Skin Added", "Skin was successfully added" },
                { "Skin Removed", "Skin was removed" }
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);

            AddCovalenceCommand(_config.Command, nameof(CommandSkin));
        }

        private void Unload()
        {
            for (var i = _boxes.Count - 1; i >= 0; i--)
            {
                var container = _boxes[i];
                OnPlayerDisconnected(container.owner);
            }
        }

        private void OnPlayerInit(Component player)
        {
            var container = player.gameObject.AddComponent<ContainerController>();
            _boxes.Add(container); // lol
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            var index = ContainerController.FindIndex(player);
            var container = _boxes[index];
            container.DoDestroy();
            _boxes.RemoveAt(index);
        }

        private void OnEntityTakeDamage(BaseNetworkable entity, HitInfo info)
        {
            if (!(entity is StorageContainer) || ContainerController.FindIndex((StorageContainer) entity) == -1)
                return;

            // Remove damage from our containers
            info.damageTypes.ScaleAll(0);
        }

        private void OnEntityDeath(BaseNetworkable entity, HitInfo info)
        {
            // Same as in OnEntityTakeDamage
            OnEntityTakeDamage(entity, info);
        }

        #region Working With Containers

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            /*
             * TODO:
             * 1. Clear and refund an item (OnLootEntityEnd)
             * 2. Add new items
             */
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            /*
             * TODO:
             * 1. Clear and refund an item (OnLootEntityEnd)
             * 2. Add new items
             */

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            /*
             * TODO:
             * 1. Clear and refund an item (OnLootEntityEnd)
             * 2. Add new items
             */
            
            // WARNING! Need to work on it and CanMoveItem, I don't think I really need this.

            return null;
        }

        private void OnItemRemovedFromContainer(ItemContainer itemContainer, Item item)
        {
            var player = itemContainer?.GetOwnerPlayer();
            var storageContainer = itemContainer?.entityOwner as StorageContainer;
            var container = ContainerController.Find(player);
            if (container == null || container.container != storageContainer)
                return;

            container.Clear(); // I guess it's all okay but needs testing
        }

        private void OnLootEntityEnd(BasePlayer player, Object entity)
        {
            var container = ContainerController.Find(player);
            if (container.container != entity)
                return;
            
            container.GiveItemsBack();
            container.Clear();
        }

        #endregion

        // I don't think we need it because we set limitNetworking to true.
        /*
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            // You won't see our containers
            if (ContainerController.FindIndex(entity as StorageContainer) != -1)
                return false;
            
            return null;
        }
        */
        
        #endregion

        #region Commands

        private void CommandSkin(IPlayer player, string command, string[] args)
        {
            if (!CanUse(player.Id))
            {
                player.Reply(GetMsg("Not Allowed", player.Id));
                return;
            }

            if (args.Length == 0)
                args = new[] {"show"}; // :P strange yeah

            var isAdmin = player.IsServer || CanUseAdmin(player.Id);
            var basePlayer = player.Object as BasePlayer;
            var isPlayer = basePlayer != null;
            
            switch (args[0].ToLower())
            {
                case "_tech-update": // TODO: Usage (UI Buttons)
                {
                    int page;
                    if (args.Length != 2 || !isPlayer || !int.TryParse(args[1], out page))
                        break;

                    var container = ContainerController.Find(basePlayer);
                    container.UpdateContent(page);
                    
                    break;
                }
                    
                case "show":
                case "s":
                {
                    if (!isPlayer)
                    {
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    var container = ContainerController.Find(basePlayer);
                    if (!container.CanShow())
                    {
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    container.Show();
                    break;
                }

                case "add":
                case "a":
                {
                    if (args.Length != 3)
                        goto default;

                    if (!isAdmin)
                    {
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    var shortname = args[1];
                    ulong skin;
                    if (!ulong.TryParse(args[2], out skin))
                    {
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }
                    
                    LoadConfig();

                    List<ulong> skins;
                    if (!_config.Skins.TryGetValue(shortname, out skins))
                        skins = new List<ulong>();

                    if (skins.Contains(skin))
                    {
                        player.Reply(GetMsg("Skin Already Exists", player.Id));
                        break;
                    }
                    
                    skins.Add(skin);
                    _config.Skins[shortname] = skins;
                    player.Reply(GetMsg("Skin Added", player.Id));
                    
                    SaveConfig();
                    
                    break;
                }

                case "remove":
                case "delete":
                case "r":
                case "d":
                {
                    if (args.Length != 3)
                        goto default;

                    if (!isAdmin)
                    {
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    var shortname = args[1];
                    ulong skin;
                    if (!ulong.TryParse(args[2], out skin))
                    {
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }
                    
                    LoadConfig();

                    List<ulong> skins;
                    int index;
                    if (!_config.Skins.TryGetValue(shortname, out skins) || (index = skins.IndexOf(skin)) == -1)
                    {
                        player.Reply(GetMsg("Skin Does Not Exist", player.Id));
                        break;
                    }
                    
                    skins.RemoveAt(index);
                    _config.Skins[shortname] = skins;
                    player.Reply(GetMsg("Skin Removed", player.Id));
                    
                    SaveConfig();
                    
                    break;
                }

                case "get":
                case "g":
                {
                    if (!isPlayer)
                    {
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    var item = basePlayer.GetActiveItem();
                    if (item == null || !item.IsValid())
                    {
                        player.Reply(GetMsg("Skin Get No Item", player.Id));
                        break;
                    }

                    player.Reply(GetMsg("Skin Get Format", player.Id).Replace("{shortname}", item.info.shortname)
                        .Replace("{id}", item.skin.ToString()));
                    
                    break;
                }

                default: // and "help", and all other args
                {
                    player.Reply(GetMsg(isAdmin ? "Admin Help" : "Help", player.Id));
                    break;
                }
            }
        }

        #endregion
        
        #region Controller

        private class ContainerController : MonoBehaviour
        {
            /*
             * Basic tips:
             * Item with index 0: Player's skin item
             */
            
            public BasePlayer owner;
            public StorageContainer container;
            public ItemContainer inventory => container.inventory;
            
            // TODO: No StorageContainer, only ItemContainer

            private void Awake()
            {
                container = GameManager.server.CreateEntity(BoxPrefab) as StorageContainer;
                if (container == null)
                    return; // Just a useless check, it shouldn't be null :)

                container.limitNetworking = true; // No-one shouldn't really see it.
                container.Spawn(); // Spawning it, YES!
            }    

            #region Search

            // ReSharper disable once SuggestBaseTypeForParameter
            public static int FindIndex(BasePlayer player)
            {
                if (!CanShow(player))
                    goto none;
                
                for (var i = 0; i < _boxes.Count; i++)
                {
                    if (_boxes[i].owner == player)
                    {
                        return i;
                    }
                }

                none:
                return -1;
            }

            public static ContainerController Find(BasePlayer player)
            {
                var index = FindIndex(player);
                return index == -1 ? null : _boxes[index];
            }

            // ReSharper disable once SuggestBaseTypeForParameter
            public static int FindIndex(StorageContainer container)
            {
                if (container == null)
                    goto none;
                
                for (var i = 0; i < _boxes.Count; i++)
                {
                    if (_boxes[i].container == container)
                    {
                        return i;
                    }
                }

                none:
                return -1;
            }

            public static ContainerController Find(StorageContainer container)
            {
                var index = FindIndex(container);
                return index == -1 ? null : _boxes[index];
            }
            
            #endregion

            public void ChangeTo(Item item)
            {
                GiveItemsBack();
                inventory.Insert(item);
                UpdateContent(0);
            }

            public void Show()
            {
                owner.EndLooting();
                
                UpdateContent(0);
                if (!owner.inventory.loot.StartLootingEntity(container, false))
                    return;
                
                owner.inventory.loot.AddContainer(inventory);
                owner.inventory.loot.SendImmediate();
                owner.ClientRPCPlayer(null, owner, "RPC_OpenLootPanel", container.GetPanelName());
            }

            public bool CanShow()
            {
                return CanShow(owner);
            }

            private static bool CanShow(BaseCombatEntity player)
            {
                return player != null && !player.IsDead();
            }

            public void GiveItemsBack()
            {
                if (owner == null || !IsValid())
                    return;

                var item = inventory?.GetSlot(0);
                if (item == null)
                    return;
                
                owner.GiveItem(item);
            }

            public void Clear()
            {
                inventory.Clear();
                ItemManager.DoRemoves();
                inventory.itemList.Clear();
            }

            public void CloseContainer()
            {
                owner.EndLooting();
            }

            public void DoDestroy()
            {
                GiveItemsBack();
                container.Kill();
                Destroy(this);
            }

            public bool IsValid() => container != null && container.inventory?.itemList != null;

            public void UpdateContent(int page)
            {
                Clear();
                
                if (page < 0 || !IsValid() || inventory.itemList.Count <= 0)
                    return;

                var item = inventory.GetSlot(0);
                List<ulong> skins;
                if (!_config.Skins.TryGetValue(item.info.shortname, out skins))
                    return;
                
                var perPage = inventory.capacity - 1;
                var offset = perPage * page;
                if (offset >= skins.Count)
                    return;
                
                for (var i = 0; i < inventory.itemList.Count; i++)
                {
                    if (i == 0)
                        continue;

                    inventory.itemList[i].DoRemove();
                    inventory.itemList.RemoveAt(i);
                }

                var slot = 1;
                for (var i = 0; i < skins.Count; i++)
                {
                    if (offset > i)
                        continue;
                    
                    var skin = skins[i];
                    var newItem = ItemManager.Create(item.info, item.amount, skin);
                    
                    newItem.RemoveFromContainer();
                    newItem.RemoveFromWorld();
                    newItem.position = slot++;

                    newItem.parent = inventory;
                    inventory.itemList.Add(newItem);
                    foreach (var itemMod in newItem.info.itemMods)
                        itemMod.OnParentChanged(newItem);
                }
            }
        }
        
        #endregion
        
        #region Helpers

        private bool CanUse(string id) => permission.UserHasPermission(id, PermissionUse);

        private bool CanUseAdmin(string id) => permission.UserHasPermission(id, PermissionAdmin);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}