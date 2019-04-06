using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Skins", "Iv Misticos", "2.0.0")]
    [Description("Allow players to change items skin with the skin from steam workshop.")]
    class Skins : RustPlugin
    {
        private const string BoxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private static List<ContainerController> _boxes = new List<ContainerController>();

        private const string PermissionUse = "skins.use";
        private const string PermissionAdmin = "skins.admin";

        #region Configuration

        private Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Chat Command")]
            public string CommandChat = "skin";
            
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
                { "Not Allowed", "You don't have permission to use this command" }
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
        }

        private void Unload()
        {
            foreach (var container in _boxes)
            {
                // TODO
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            
        }

        private void OnEntityTakeDamage(BaseNetworkable entity, HitInfo info)
        {
            if (ContainerController.FindIndex(entity as StorageContainer) == -1)
                return;

            // Remove damage from our containers
            info.damageTypes.ScaleAll(0);
        }

        #region Working With Containers

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            // TODO
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            // TODO
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            // TODO
        }

        private void OnItemSplit(Item item, int amount)
        {
            // TODO
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            // TODO
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            // TODO
        }

        #endregion

        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            // You won't see our containers
            if (ContainerController.FindIndex(entity as StorageContainer) != -1)
                return false;
            
            return null;
        }
        
        #endregion

        #region Commands

        private void CommandWorkshopLoad(ConsoleSystem.Arg arg)
        {
            // TODO
        }

        private void CommandSkin(BasePlayer player)
        {
            // TODO
        }
        
        #endregion
        
        #region Controller

        private class ContainerController : MonoBehaviour
        {
            public BasePlayer owner;
            public StorageContainer container;

            private void Awake()
            {
                container = GameManager.server.CreateEntity(BoxPrefab) as StorageContainer;
                if (container == null)
                    return; // Just a useless check, it shouldn't be null :)
                
                container.Spawn(); // Spawning it, YES!
            }

            #region Search

            // ReSharper disable once SuggestBaseTypeForParameter
            public static int FindIndex(BasePlayer player)
            {
                for (var i = 0; i < _boxes.Count; i++)
                {
                    if (_boxes[i].owner == player)
                    {
                        return i;
                    }
                }

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
                for (var i = 0; i < _boxes.Count; i++)
                {
                    if (_boxes[i].container == container)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public static ContainerController Find(StorageContainer container)
            {
                var index = FindIndex(container);
                return index == -1 ? null : _boxes[index];
            }
            
            #endregion
        }
        
        #endregion
        
        #region Helpers

        private bool CanUse(ulong id) => permission.UserHasPermission(id.ToString(), PermissionUse);

        private bool CanUseAdmin(ulong id) => permission.UserHasPermission(id.ToString(), PermissionAdmin);

        #endregion
    }
}