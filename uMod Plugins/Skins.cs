using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Skins", "Iv Misticos", "2.0.0")]
    [Description("Allow players to change items skin with the skin from steam workshop.")]
    class Skins : RustPlugin
    {
        private const string BoxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private HashSet<StorageContainer> _boxes = new HashSet<StorageContainer>();

        private const string PermissionUse = "skins.use";
        private const string PermissionAdmin = "skins.admin";

        #region Configuration

        private Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Chat Command")]
            public string CommandChat = "skin";
            
            [JsonProperty(PropertyName = "")]
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

        private void OnEntityTakeDamage(BaseNetworkable entity, HitInfo info)
        {
            if(_boxes.Contains(entity))
            {
                // Remove damage from our containers
                info.damageTypes.ScaleAll(0);
            }
        }

        #region Working With Containers

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            
        }

        private void OnItemSplit(Item item, int amount)
        {
            
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            
        }

        #endregion

        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            // You won't see our containers
            if (_boxes.Contains(entity))
                return false;
            
            return null;
        }
        
        #endregion

        #region Commands

        private void CommandWorkshopLoad(ConsoleSystem.Arg arg)
        {
            
        }

        private void CommandSkin(BasePlayer player)
        {
            
        }
        
        #endregion
        
        #region Helpers

        private bool CanUse(ulong id) => permission.UserHasPermission(id.ToString(), PermissionUse);

        private bool CanUseAdmin(ulong id) => permission.UserHasPermission(id.ToString(), PermissionAdmin);

        private StorageContainer GetContainer()
        {
            var container = GameManager.server.CreateEntity(BoxPrefab) as StorageContainer;
            if (container == null)
                return null;

            _boxes.Add(container);
            container.Spawn();
            
            return container;
        }

        #endregion
    }
}