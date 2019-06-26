using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Object = UnityEngine.Object;

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

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
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

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerInit(player);
            }
        }

        private void Unload()
        {
            for (var i = _controllers.Count - 1; i >= 0; i--)
            {
                var container = _controllers[i];
                container.Destroy();
                
                _controllers.RemoveAt(i);
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            _controllers.Add(new ContainerController(player)); // lol
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            var index = ContainerController.FindIndex(player);
            var container = _controllers[index];
            container.Destroy();
            
            _controllers.RemoveAt(index);
        }

        #region Working With Containers

        private void OnItemAddedToContainer(ItemContainer itemContainer, Item item)
        {
            var player = itemContainer.GetOwnerPlayer();
            var container = ContainerController.Find(itemContainer);
            if (container == null || player != null)
                return;
            
            PrintDebug("OnItemAddedToContainer");
            item.position = 0;
            container.UpdateContent(0);
        }

        private void OnItemRemovedFromContainer(ItemContainer itemContainer, Item item)
        {
            var player = itemContainer.GetOwnerPlayer();
            var container = ContainerController.Find(itemContainer);
            if (container == null || player == null)
                return;
            
            PrintDebug("OnItemRemovedFromContainer");
            container.Clear();
        }

        private void OnPlayerLootEnd(PlayerLoot loot)
        {
            PrintDebug("OnLootEntityEnd");

            var player = loot.gameObject.GetComponent<BasePlayer>();
            if (player != loot.entitySource)
                return;
            
            var container = ContainerController.Find(player);
            if (container == null)
                return;
            
            PrintDebug("Ended looting container");
            container.Close();
        }

        private object CanLootPlayer(BasePlayer looter, Object target)
        {
            if (looter != target)
                return null;

            var container = ContainerController.Find(looter);
            if (container == null || !container.IsOpened)
                return null;

            PrintDebug("Allowing to loot player (skin container)");
            return true;
        }

        #endregion
        
        #endregion

        #region Commands

        private void CommandSkin(IPlayer player, string command, string[] args)
        {
            PrintDebug("Executed Skin command");
            
            if (!CanUse(player))
            {
                PrintDebug("Not allowed");
                player.Reply(GetMsg("Not Allowed", player.Id));
                return;
            }

            if (args.Length == 0)
                args = new[] {"show"}; // :P strange yeah

            var isAdmin = player.IsServer || CanUseAdmin(player);
            var basePlayer = player.Object as BasePlayer;
            var isPlayer = basePlayer != null;
            
            PrintDebug($"Arguments: {string.Join(" ", args)}");
            PrintDebug($"Is Admin: {isAdmin} : Is Player: {isPlayer}");
            
            switch (args[0].ToLower())
            {
                case "_tech-update":
                {
                    int page;
                    if (args.Length != 2 || !isPlayer || !int.TryParse(args[1], out page))
                        break;

                    var container = ContainerController.Find(basePlayer);

                    container?.UpdateContent(page);
                    container?.DestroyUI();
                    container?.DrawUI(page);
                    break;
                }
                    
                case "show":
                case "s":
                {
                    if (!isPlayer)
                    {
                        PrintDebug("Not a player");
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    var container = ContainerController.Find(basePlayer);
                    if (container == null || !container.CanShow())
                    {
                        PrintDebug("Cannot show container or container not found");
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    timer.Once(1f, () => container.Show());
                    break;
                }

                case "add":
                case "a":
                {
                    if (args.Length != 3)
                        goto default;

                    if (!isAdmin)
                    {
                        PrintDebug("Not an admin");
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    var shortname = args[1];
                    ulong skin;
                    if (!ulong.TryParse(args[2], out skin))
                    {
                        PrintDebug("Invalid skin");
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }
                    
                    LoadConfig();

                    List<ulong> skins;
                    if (!_config.Skins.TryGetValue(shortname, out skins))
                        skins = new List<ulong>();

                    if (skins.Contains(skin))
                    {
                        PrintDebug("Skin already exists");
                        player.Reply(GetMsg("Skin Already Exists", player.Id));
                        break;
                    }
                    
                    skins.Add(skin);
                    _config.Skins[shortname] = skins;
                    player.Reply(GetMsg("Skin Added", player.Id));
                    
                    SaveConfig();
                    PrintDebug("Added skin");
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
                        PrintDebug("Not an admin");
                        player.Reply(GetMsg("Not Allowed", player.Id));
                        break;
                    }

                    var shortname = args[1];
                    ulong skin;
                    if (!ulong.TryParse(args[2], out skin))
                    {
                        PrintDebug("Invalid skin");
                        player.Reply(GetMsg("Incorrect Skin", player.Id));
                        break;
                    }
                    
                    LoadConfig();

                    List<ulong> skins;
                    int index;
                    if (!_config.Skins.TryGetValue(shortname, out skins) || (index = skins.IndexOf(skin)) == -1)
                    {
                        PrintDebug("Skin doesnt exist");
                        player.Reply(GetMsg("Skin Does Not Exist", player.Id));
                        break;
                    }
                    
                    skins.RemoveAt(index);
                    _config.Skins[shortname] = skins;
                    player.Reply(GetMsg("Skin Removed", player.Id));
                    
                    SaveConfig();
                    PrintDebug("Removed skin");
                    break;
                }

                case "get":
                case "g":
                {
                    if (!isPlayer)
                    {
                        PrintDebug("Not a player");
                        player.Reply(GetMsg("Cannot Use", player.Id));
                        break;
                    }

                    var item = basePlayer.GetActiveItem();
                    if (item == null || !item.IsValid())
                    {
                        PrintDebug("Invalid item");
                        player.Reply(GetMsg("Skin Get No Item", player.Id));
                        break;
                    }

                    player.Reply(GetMsg("Skin Get Format", player.Id).Replace("{shortname}", item.info.shortname)
                        .Replace("{id}", item.skin.ToString()));
                    
                    break;
                }

                default: // and "help", and all other args
                {
                    PrintDebug("Unknown command");
                    player.Reply(GetMsg(isAdmin ? "Admin Help" : "Help", player.Id));
                    break;
                }
            }
        }

        #endregion
        
        #region Controller

        private class ContainerController
        {
            /*
             * Basic tips:
             * Item with slot 0: Player's skin item
             */

            private const int Capacity = 30;
            
            public BasePlayer Owner;
            public ItemContainer Container;
            public bool IsOpened = false;

            #region Search

            // ReSharper disable once SuggestBaseTypeForParameter
            public static int FindIndex(BasePlayer player)
            {
                if (!CanShow(player))
                    goto none;
                
                for (var i = 0; i < _controllers.Count; i++)
                {
                    if (_controllers[i].Owner == player)
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
                    if (_controllers[i].Container == container)
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

            public ContainerController(BasePlayer player)
            {
                Owner = player;
                
                Container = new ItemContainer
                {
                    entityOwner = Owner,
                    capacity = Capacity,
                    isServer = true,
                    allowedContents = ItemContainer.ContentsType.Generic
                };
                
                Container.GiveUID();
            }

            public void DestroyUI()
            {
                PrintDebug("Started UI destroy");
                CuiHelper.DestroyUi(Owner, "Skins.Background");
            }

            public void DrawUI(int page)
            {
                PrintDebug("Started UI draw");
                var elements = new CuiElementContainer();
                const string anchorCorner = "1.0 1.0";

                var background = new CuiElement
                {
                    Name = "Skins.Background",
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.18 0.28 0.36"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchorCorner,
                            AnchorMax = anchorCorner,
                            OffsetMin = "-300 -100",
                            OffsetMax = "0 0"
                        }
                    }
                };

                var left = new CuiElement
                {
                    Name = "Skins.Left",
                    Parent = background.Name,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Close = background.Name,
                            Command = $"{_config.Command} _tech-update {page - 1}",
                            Color = "0.11 0.51 0.83"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.025 0.05",
                            AnchorMax = "0.325 0.95"
                        }
                    }
                };

                var leftText = new CuiElement
                {
                    Name = "Skins.Left.Text",
                    Parent = left.Name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "<"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                };

                var center = new CuiElement
                {
                    Name = "Skins.Center",
                    Parent = background.Name,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.11 0.51 0.83"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.350 0.05",
                            AnchorMax = "0.650 0.95"
                        }
                    }
                };

                var centerText = new CuiElement
                {
                    Name = "Skins.Center.Text",
                    Parent = center.Name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{page}"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                };

                var right = new CuiElement
                {
                    Name = "Right.Left",
                    Parent = background.Name,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Close = background.Name,
                            Command = $"{_config.Command} _tech-update {page - 1}",
                            Color = "0.11 0.51 0.83"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.675 0.05",
                            AnchorMax = "0.975 0.95"
                        }
                    }
                };

                var rightText = new CuiElement
                {
                    Name = "Skins.Right.Text",
                    Parent = right.Name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = ">"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                };
                
                elements.Add(background);
                elements.Add(left);
                elements.Add(leftText);
                elements.Add(center);
                elements.Add(centerText);
                elements.Add(right);
                elements.Add(rightText);

                PrintDebug("Started UI send");
                CuiHelper.AddUi(Owner, elements);
                PrintDebug("UI sent");
            }

            public void Close()
            {
                PrintDebug("Closing container");
                
                GiveItemBack();
                Clear();
                DestroyUI();
                
                IsOpened = false;
            }

            public void Show()
            {
                PrintDebug("Started container Show");
                
                IsOpened = true;
                UpdateContent(0);
                
                var loot = Owner.inventory.loot;
                
                loot.Clear();
                loot.PositionChecks = false;
                loot.entitySource = Owner;
                loot.itemSource = null;
                loot.AddContainer(Container);
                loot.SendImmediate();
                
                Owner.ClientRPCPlayer(null, Owner, "RPC_OpenLootPanel", "genericlarge");
            }
            
            #region Can Show

            public bool CanShow()
            {
                return CanShow(Owner);
            }

            private static bool CanShow(BaseCombatEntity player)
            {
                return player != null && !player.IsDead();
            }
            
            #endregion

            public void GiveItemBack()
            {
                if (!IsValid())
                    return;
                
                PrintDebug("Trying to give item back..");

                var item = Container.GetSlot(0);
                if (item == null)
                {
                    PrintDebug("Invalid item");
                    return;
                }

                Owner.GiveItem(item);
                PrintDebug("Gave item back");
            }

            public void Clear()
            {
                PrintDebug($"Clearing container");
                
                for (var i = 0; i < Container.itemList.Count; i++)
                {
                    var item = Container.itemList[i];
                    item.DoRemove();
                }
                
                Container.itemList.Clear();
                Container.MarkDirty();
            }

            public void Destroy()
            {
                Close();
                Container.Kill();
                
                PrintDebug("Destroyed container");
            }

            public void UpdateContent(int page)
            {
                var source = Container.GetSlot(0);
                source?.MarkDirty();

                /*
                PrintDebug($"Updating content ({page} page)");
                Clear(false);

                if (page < 0 || !IsValid() || container.itemList.Count <= 0)
                {
                    PrintDebug("Invalid page / items / etc");
                    return;
                }

                if (isOpened)
                {
                    PrintDebug("Opened. Drawing UI");
                    DrawUI(page);
                }

                var item = container.GetSlot(0);
                List<ulong> skins;
                if (!_config.Skins.TryGetValue(item.info.shortname, out skins))
                {
                    PrintDebug("Cannot find skins");
                    return;
                }

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
                */
            }

            public Item GetDuplicateItem(Item item, ulong skin)
            {
                PrintDebug($"Getting duplicate for {item.info.shortname}..");

                var newItem = ItemManager.Create(item.info, item.amount, skin);
                newItem._maxCondition = item._maxCondition;
                newItem._condition = item._condition;
                newItem.contents.capacity = item.contents.capacity;
                
                return newItem;
            }

            private bool IsValid() => Owner == null || Container?.itemList != null;
        }
        
        #endregion
        
        #region Helpers

        private bool CanUse(IPlayer player) => player.HasPermission(PermissionUse);

        private bool CanUseAdmin(IPlayer player) => player.HasPermission(PermissionAdmin);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }

        #endregion
    }
}