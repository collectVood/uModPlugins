using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        private Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Command")]
            public string Command = "skin";
            
            [JsonProperty(PropertyName = "")] // no idea what it was
            public bool DefaultSkins = false;
            
            [JsonProperty(PropertyName = "Custom Skins")]
            public Dictionary<string, List<ulong>> CustomSkins = new Dictionary<string, List<ulong>>
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
                          "skin show - Show skins" },
                { "Admin Help", "Admin command usage:\n" +
                          "skin show - Show skins\n" +
                          "skin remove (Shortname) (Skin ID) - Remove a skin\n" +
                          "skin add (Shortname) (Skin ID) - Add a skin" },
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

        private void CommandWorkshopLoad(IPlayer player, string command, string[] args)
        {
            // TODO
        }

        private void CommandSkin(IPlayer player, string command, string[] args)
        {
            if (!CanUse(player.Id))
            {
                player.Reply(GetMsg("Not Allowed", player.Id));
                return;
            }

            if (args.Length == 0)
                args = new[] {"show"}; // :P strange yeah

            var isAdmin = player.IsServer || player.HasPermission(PermissionAdmin);
            var basePlayer = player.Object as BasePlayer;
            var isPlayer = basePlayer != null;
            
            switch (args[0].ToLower())
            {
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
                    
                    // TODO: Add skin
                    
                    break;
                }

                case "remove":
                case "delete":
                case "r":
                case "d":
                {
                    if (args.Length != 3)
                        goto default;
                    
                    // TODO: Remove skin
                    
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

            public void Show()
            {
                owner.EndLooting();
                
                if (!owner.inventory.loot.StartLootingEntity(container, false))
                    return;
                
                owner.inventory.loot.AddContainer(container.inventory);
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
                if (owner == null || container == null)
                    return;

                var item = container.inventory?.GetSlot(0);
                if (item == null)
                    return;
                
                owner.GiveItem(item);
            }

            public void Clear()
            {
                container.inventory.Clear();
                ItemManager.DoRemoves();
                container.inventory.itemList.Clear();
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
        }
        
        #endregion
        
        #region Helpers

        private bool CanUse(string id) => permission.UserHasPermission(id, PermissionUse);

        private bool CanUseAdmin(string id) => permission.UserHasPermission(id, PermissionAdmin);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}