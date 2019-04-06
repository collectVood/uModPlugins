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
        private HashSet<uint> _boxes = new HashSet<uint>();

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
//                ["NotAllowed"] = "You don't have permission to use this command.",
//                ["ConsoleIncorrectSkinIdFormat"] = "Wrong format for the skinId, this must be numbers.",
//                ["ConsoleItemIsNotFound"] = "Item with shortname {0} is not found.",
//                ["ConsoleItemSkinExist"] = "The skinId {0} is already exist for item {1}",
//                ["ConsoleItemAdded"] = "A new skinId {0} added for item {1}",
//                ["ConsoleRemoveItemNotFound"] = "The item {0} is not found in config file. Nothing to remove.",
//                ["ConsoleRemoveSkinNotFound"] = "The skinId {0} is not found in config file. Nothing to remove.",
//                ["ConsoleRemoveSkinRemoved"] = "The skinId {0} is found in config file and removed.",
//                ["ConsoleListItemNotFound"] = "The item {0} is not found in config file.",
//                ["ConsoleWorkshopLoad"] = "Start to load workshop skins, it'll take some time, please wait.",
//                ["ConsoleWorkshopLoaded"] = "The {0} new skins is loaded and append to config file.",
//                ["ConsoleUniqueSort"] = "All skin duplicates was removed.",
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
//            var ToClose = new List<SkinContainer>();
//            foreach (var container in Containers)
//                if (container.status != ContainerStatus.Ready)
//                    ToClose.Add(container);
//
//            foreach(var container in ToClose)
//                OnLootEntityEnd(container.inventory.playerOwner, container.storage as BaseCombatEntity);
        }

        private void OnEntityTakeDamage(BaseNetworkable entity, HitInfo info)
        {
            if(_boxes.Contains(entity.net.ID))
            {
                // Remove damage from our containers
                info.damageTypes.ScaleAll(0);
            }
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
//            if (Containers.Exists(c => c.inventory == container))
//            {
//                var _skinContainer = Containers.Find(c => c.inventory == container);
//                if (_skinContainer.status == ContainerStatus.Ready)
//                {
//                    var owner = _skinContainer.storage.inventory.playerOwner;
//                    if (!SkinsList.ContainsKey(item.info.shortname))
//                        LoadSkinsList(item.info);
//
//                    if (!SkinsList.ContainsKey(item.info.shortname) || SkinsList[item.info.shortname].Count <= 1)
//                    {
//                        item.MoveToContainer(owner.inventory.containerMain);
//                        return;
//                    }
//
//                    _skinContainer.status = ContainerStatus.FilledIn;
//
//                    var ammo = item.GetHeldEntity() as BaseProjectile;
//                    if (ammo != null)
//                    {
//                        ammo.UnloadAmmo(item, owner);
//                        ammo.primaryMagazine.contents = 0;
//                    }
//
//                    if (item.contents != null)
//                    {
//                        var mods = new List<global::Item>();
//                        foreach (var mod in item.contents.itemList)
//                            if (mod != null)
//                                mods.Add(mod);
//
//                        foreach (var mod in mods)
//                            MoveItemBack(mod, owner);
//                    }
//                    item.RemoveFromContainer();
//
//                    NextTick(() =>
//                    {
//                        _skinContainer.storage.inventory.capacity = SkinsList[item.info.shortname].Count;
//                        foreach (int skinId in SkinsList[item.info.shortname])
//                        {
//                            var i = ItemManager.CreateByItemID(item.info.itemid, item.amount, (ulong)skinId);
//                            i.condition = item.condition;
//                            NextTick(() =>
//                            {
//                                var a = i.GetHeldEntity() as BaseProjectile;
//                                if (a != null)
//                                {
//                                    a.primaryMagazine.contents = 0;
//                                }
//                                _skinContainer.ItemsList.Add(i);
//                                i.MoveToContainer(_skinContainer.storage.inventory, -1, false);
//                                NextTick(() => { _skinContainer.status = ContainerStatus.Filled; });
//                            });
//                        }
//                    });
//                }
//            }
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
//            if(Containers.Exists(c => c.uid == item.GetRootContainer().uid))
//            {
//                if (playerLoot.containerMain.uid == targetContainer && playerLoot.containerMain.GetSlot(targetSlot) != null)
//                    return false;
//                if (playerLoot.containerBelt.uid == targetContainer && playerLoot.containerBelt.GetSlot(targetSlot) != null)
//                    return false;
//                if (playerLoot.containerWear.uid == targetContainer && playerLoot.containerWear.GetSlot(targetSlot) != null)
//                    return false;
//            }
//
//            var targetSkinContainer = Containers.Find(c => c.uid == targetContainer);
//            if(targetSkinContainer != null)
//            {
//                if(targetSkinContainer.status != ContainerStatus.Ready)
//                {
//                    return false;
//                }
//            }
//            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
//            if (Containers.Exists(c => c.inventory == container && c.status != ContainerStatus.Ready))
//            {
//                return false;
//            }
//            return true;
        }

        private void OnItemSplit(Item item, int amount)
        {
            if (Containers.Exists(c => c.inventory == item.GetRootContainer() && c.status != ContainerStatus.FilledIn))
            {
                NextTick(() =>
                {
                    var _skinContainer = Containers.Find(c => c.inventory == item.GetRootContainer());
                    var new_amount = 0;
                    foreach (var i in _skinContainer.inventory.itemList)
                        new_amount = (new_amount == 0) ? i.amount : (i.amount < new_amount) ? i.amount : new_amount;
                    foreach (var i in _skinContainer.inventory.itemList)
                        i.amount = new_amount;
                });
            }
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (Containers.Exists(c => c.inventory == container))
            {
                var _skinContainer = Containers.Find(c => c.inventory == container);
                if (_skinContainer.status == ContainerStatus.Filled)
                {
                    foreach(var i in _skinContainer.ItemsList)
                    {
                        if (i == item) continue;
                        i.Remove(0f);
                    }
                    _skinContainer.ItemsList = new List<Item>();
                    _skinContainer.storage.inventory.capacity = 1;
                    _skinContainer.status = ContainerStatus.Ready;
                }
            }
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            var container = Containers.Find(c => c.hashCode == entity.GetHashCode());
            if(container != null)
            {
                if(container.status != ContainerStatus.Ready)
                {
                    var item = container.inventory.GetSlot(0);
                    if(item != null) container.inventory.GetOwnerPlayer().GiveItem(item);
                }
                container.storage.KillMessage();
                _boxes.Remove(entity.GetHashCode());
                Containers.Remove(container);
            }
        }

        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            // You won't see our containers
            if (_boxes.Contains(entity.net.ID))
                return false;
            
            return null;
        }
        
        #endregion

        #region Commands

        private void CommandWorkshopLoad(ConsoleSystem.Arg arg)
        {
//            var player = arg.Player();
//            if (player is BasePlayer && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.admin"))
//            {
//                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
//            }
//
//            var userIDString = (player is BasePlayer) ? player.UserIDString : null;
//
//            SendReply(arg, lang.GetMessage("ConsoleWorkshopLoad", this, userIDString));
//
//            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", string.Empty, (code, response) =>
//            {
//                if (!(response == null && code == 200))
//                {
//                    var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
//                    var items = schema.items;
//                    var added_count = 0;
//                    foreach (var item in items)
//                    {
//                        if(item.workshopid != null && item.itemshortname != null)
//                        {
//                            var workshopid = ulong.Parse(item.workshopid);
//                            var shortname = item.itemshortname;
//
//                            if (!_config.CustomSkins.ContainsKey(shortname))
//                                _config.CustomSkins.Add(shortname, new List<ulong>());
//
//                            if (!_config.CustomSkins[shortname].Contains(workshopid))
//                            {
//                                _config.CustomSkins[item.itemshortname].Add(workshopid);
//                                added_count += 1;
//                            }
//
//                            if (!SkinsList.ContainsKey(item.itemshortname))
//                                SkinsList.Add(item.itemshortname, new List<ulong>());
//
//                            if (!SkinsList[shortname].Contains(workshopid))
//                                SkinsList[shortname].Add(workshopid);
//                        }
//                    }
//                    SaveConfig(_config);
//                    SendReply(arg, lang.GetMessage("ConsoleWorkshopLoaded", this, userIDString), added_count);
//                }
//            }, this);
        }

        private void CommandSkin(BasePlayer player)
        {
//            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "skins.allow"))
//            {
//                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
//                return;
//            }
//            var container = SpawnContainer(player);
//            timer.In(0.25f, () =>
//            {
//                PlayerLootContainer(player, container.storage);
//            });
        }
        
        #endregion
    }
}