using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Component = UnityEngine.Component;

namespace Oxide.Plugins
{
    [Info("Skins", "Iv Misticos", "2.0.0")]
    [Description("Allow players to change items skin with the skin from steam workshop.")]
    class Skins : RustPlugin
    {
        #region Variables
        
        private static List<ContainerController> _controllers = new List<ContainerController>();

        private const string PermissionUse = "skins.use";
        private const string PermissionAdmin = "skins.admin";

        private Coroutine _skinsValidation;
        
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
                                "skin add (Shortname) (Skin ID) - Add a skin\n" +
                                "skin validate - Validate skins" },
                { "Skin Get Format", "{shortname}'s skin: {id}" },
                { "Skin Get No Item", "Please, hold the needed item" },
                { "Incorrect Skin", "You have entered an incorrect skin" },
                { "Skin Already Exists", "This skin already exists on this item" },
                { "Skin Does Not Exist", "This skin does not exist" },
                { "Skin Added", "Skin was successfully added" },
                { "Skin Removed", "Skin was removed" },
                { "Validation: Started", "Validating skins.." },
                { "Validation: Ended", "Invalid skins removed: {removed}" }
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
            for (var i = _controllers.Count - 1; i >= 0; i--)
            {
                var container = _controllers[i];
                OnPlayerDisconnected(container.owner);
            }

            ValidateSkinsStop();
        }

        private void OnPlayerInit(Component player)
        {
            var container = player.gameObject.AddComponent<ContainerController>();
            _controllers.Add(container); // lol
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            var index = ContainerController.FindIndex(player);
            var container = _controllers[index];
            container.DoDestroy();
            _controllers.RemoveAt(index);
        }

        private void OnEntityTakeDamage(BaseNetworkable entity, HitInfo info)
        {
            if (!(entity is StorageContainer) ||
                ContainerController.FindIndex(((StorageContainer) entity).inventory) == -1)
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

        private object CanAcceptItem(ItemContainer itemContainer, Item item)
        {
            var container = ContainerController.Find(itemContainer);
            if (container == null)
                return null;

            container.isOpened = true;
            container.GiveItemsBack();
            container.Clear();
            container.ChangeTo(item);

            return true;
        }

        private void OnItemRemovedFromContainer(ItemContainer itemContainer, Item item)
        {
            var player = itemContainer?.GetOwnerPlayer();
            var container = ContainerController.Find(player);
            if (container == null || container.container != itemContainer)
                return;

            container.Clear(true); // I guess it's all okay but needs testing
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (!(entity is StorageContainer))
                return;

            var storageContainer = (StorageContainer) entity;
            
            var container = ContainerController.Find(player);
            if (container.container != storageContainer.inventory)
                return;
            
            container.Close();
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
                case "_tech-update":
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

                case "validate":
                case "v":
                {
                    if (!isAdmin)
                    {
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }
                    
                    ValidateSkinsHelper(player);
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
             * Item with slot 0: Player's skin item
             */
            
            public BasePlayer owner;
            public ItemContainer container = new ItemContainer();
            public bool isOpened = false;

            #region Search

            // ReSharper disable once SuggestBaseTypeForParameter
            public static int FindIndex(BasePlayer player)
            {
                if (!CanShow(player))
                    goto none;
                
                for (var i = 0; i < _controllers.Count; i++)
                {
                    if (_controllers[i].owner == player)
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
                return index == -1 ? null : _controllers[index];
            }

            // ReSharper disable once SuggestBaseTypeForParameter
            public static int FindIndex(ItemContainer container)
            {
                if (container == null)
                    goto none;
                
                for (var i = 0; i < _controllers.Count; i++)
                {
                    if (_controllers[i].container == container)
                    {
                        return i;
                    }
                }

                none:
                return -1;
            }

            public static ContainerController Find(ItemContainer container)
            {
                var index = FindIndex(container);
                return index == -1 ? null : _controllers[index];
            }
            
            #endregion

            public void DestroyUi()
            {
                CuiHelper.DestroyUi(owner, "Skins.Left");
                CuiHelper.DestroyUi(owner, "Skins.Right");
            }

            public void DrawUI(int page)
            {
                // TODO
                /*
                var elements = new CuiElementContainer();
                const int slotHeight = 115;
                const int slotWidth = 115;
                const int distanceBetweenSlots = 7;
                const int distanceFromDownCorner = 34;
                const string midAnchor = "0.5 0.0";

                var back = new CuiElement
                {
                    Name = "Skins.Left",
                    Parent = "Hud",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = midAnchor,
                            AnchorMax = midAnchor,
                        }
                    }
                };
                */
            }

            public void Close()
            {
                isOpened = false;
                GiveItemsBack();
                Clear(true);
                DestroyUi();
            }

            public void ChangeTo(Item item)
            {
                GiveItemsBack();
                container.Insert(item);
                UpdateContent(0);
            }

            public void Show()
            {
                owner.EndLooting();
                
                UpdateContent(0);

                var loot = owner.inventory.loot;
                
                loot.Clear();
                loot.PositionChecks = false;
                loot.entitySource = null;
                loot.itemSource = null;
                loot.MarkDirty();
                
                owner.inventory.loot.AddContainer(container);
                owner.inventory.loot.SendImmediate();
                owner.ClientRPCPlayer(null, owner, "RPC_OpenLootPanel", "generic");
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

                var item = container?.GetSlot(0);
                if (item == null)
                    return;
                
                owner.GiveItem(item);
            }

            public void Clear(bool removeFirst)
            {
                for (var i = removeFirst ? 0 : 1; i < container.itemList.Count; i++)
                {
                    var item = container.itemList[i];
                    item.DoRemove();
                }

                container.itemList.Clear();
            }

            public void DoDestroy()
            {
                GiveItemsBack();
                container.Kill();
                Destroy(this);
            }

            private bool IsValid() => container?.itemList != null;

            public void UpdateContent(int page)
            {
                Clear(false);
                
                if (page < 0 || !IsValid() || container.itemList.Count <= 0)
                    return;
                
                if (isOpened)
                    DrawUI(page);

                var item = container.GetSlot(0);
                List<ulong> skins;
                if (!_config.Skins.TryGetValue(item.info.shortname, out skins))
                    return;
                
                var perPage = container.capacity - 1;
                var offset = perPage * page;
                if (offset >= skins.Count)
                    return;
                
                for (var i = 0; i < container.itemList.Count; i++)
                {
                    if (container.itemList[i].position == 0) // :(
                        continue;

                    container.itemList[i].DoRemove();
                    container.itemList.RemoveAt(i);
                }

                var slot = 1;
                for (var i = 0; i < skins.Count; i++)
                {
                    if (slot > container.capacity)
                        break;
                    
                    if (offset > i)
                        continue;
                    
                    var skin = skins[i];
                    var newItem = GetDuplicateItem(item, skin);
                    
                    newItem.RemoveFromContainer();
                    newItem.RemoveFromWorld();
                    
                    newItem.position = slot++;
                    newItem.parent = container;
                    
                    container.itemList.Add(newItem);
                    
                    foreach (var itemMod in newItem.info.itemMods)
                        itemMod.OnParentChanged(newItem);
                }
            }

            public Item GetDuplicateItem(Item item, ulong skin)
            {
                var newItem = ItemManager.Create(item.info, item.amount, skin);
                newItem._maxCondition = item._maxCondition;
                newItem._condition = item._condition;
                newItem.contents.capacity = item.contents.capacity;

                for (var i = 0; i < item.contents.itemList.Count; i++)
                {
                    var content = item.contents.itemList[i];
                    newItem.contents.Insert(GetDuplicateItem(content, content.skin));
                }

                return newItem;
            }
        }
        
        #endregion
        
        #region Helpers

        private void ValidateSkinsStop()
        {
            if (_skinsValidation == null)
                return;
            
            Rust.Global.Runner.StopCoroutine(_skinsValidation);
            _skinsValidation = null;
        }

        private void ValidateSkinsHelper(IPlayer player)
        {
            ValidateSkinsStop();
            _skinsValidation = Rust.Global.Runner.StartCoroutine(ValidateSkins(player));
        }

        private IEnumerator ValidateSkins(IPlayer player)
        {
            player?.Reply(GetMsg("Validation: Started", player.Id));
            var removed = 0;
            
            foreach (var kvp in _config.Skins)
            {
                var query = Rust.Global.SteamServer.Workshop.CreateQuery();
                query.Page = 1;
                query.PerPage = kvp.Value.Count;
                query.FileId = kvp.Value;
                yield return new WaitWhile(() => query.IsRunning);

                for (var i = 0; i < query.Items.Length; i++)
                {
                    var item = query.Items[i];
                    if (!string.IsNullOrEmpty(item.Title) && HasNeededTags(item.Tags)) continue;
                    
                    kvp.Value.Remove(item.Id);
                    removed++;
                }
            }

            player?.Reply(GetMsg("Validation: Ended", player.Id).Replace("{removed}", $"{removed}"));
            SaveConfig();
        }

        private bool HasNeededTags(IReadOnlyList<string> tags)
        {
            for (var i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                
                if (string.Equals(tag, "version2", StringComparison.CurrentCultureIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool CanUse(string id) => permission.UserHasPermission(id, PermissionUse);

        private bool CanUseAdmin(string id) => permission.UserHasPermission(id, PermissionAdmin);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}