using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
    [Info("UIEditor", "Iv Misticos", "1.0.0")]
    class UIEditor : RustPlugin
    {
        #region Variables

        private static List<UICollection> _data = new List<UICollection>();
        private static Dictionary<BasePlayer, UICollection> _active = new Dictionary<BasePlayer, UICollection>();
        
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Help", "" },
                { "No Permission", "You don't have enough permissions!" },
                { "Collection Not Found", "Collection isn't found" },
                { "Element Not Found", "Element isn't found" },
                { "Help: Load", "/uiedit load (collection name)" },
                { "Help: Save", "/uiedit save (collection name)" },
                { "Help: New", "/uiedit new (collection name)" },
                { "Help: Add", "/uiedit add (collection name)" },
                { "Help: Remove", "/uiedit remove (collection name)" },
                { "Help: Set", "/uiedit set (collection name)" }
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission("uieditor.use", this);
            
            var cmdLib = GetLibrary<Command>();
            cmdLib.AddChatCommand("uiedit", this, CommandChatEdit);
        }

        private void Unload()
        {
            var players = BasePlayer.activePlayerList;
            var playersCount = players.Count;
            for (var i = 0; i < playersCount; i++)
            {
                OnPlayerDisconnected(players[i], string.Empty);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            UICollection collection;
            if (!_active.TryGetValue(player, out collection))
                return;

            collection.Destroy(player);
            _active.Remove(player);
        }
        
        #endregion
        
        #region Commands

        private void CommandChatEdit(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;

            if (!permission.UserHasPermission("uieditor.use", id))
            {
                player.ChatMessage(GetMsg("No Permission", id));
                return;
            }
            
            if (args.Length == 0)
            {
                player.ChatMessage(GetMsg("Help", id));
                return;
            }

            switch (args[0])
            {
                #region Load
                
                case "load":
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetMsg("Help: Load", id));
                        return;
                    }

                    var collection = UICollection.FindCollection(args[1]);
                    if (collection == null)
                    {
                        player.ChatMessage(GetMsg("Collection Not Found", id));
                        return;
                    }
                    
                    collection.Draw(player);
                    break;
                }
                
                #endregion

                #region Save
                
                case "save":
                {
                    UICollection collection;
                    if (!_active.TryGetValue(player, out collection))
                    {
                        player.ChatMessage(GetMsg("Collection Not Found", id));
                        return;
                    }
                    // TODO
                    break;
                }
                
                #endregion

                #region New
                
                case "new":
                {
                    _active.TryAdd(player, new UICollection());
                    break;
                }
                
                #endregion

                #region Add
                
                case "add":
                {
                    UICollection collection;
                    if (!_active.TryGetValue(player, out collection))
                    {
                        player.ChatMessage(GetMsg("Collection Not Found", id));
                        return;
                    }
                    
                    if (args.Length < 3)
                    {
                        player.ChatMessage(GetMsg("Help: Add", id));
                        return;
                    }

                    if (args.Length == 3)
                    {
                        var element = new CuiElement();
                        element.Name = args[1];
                        element.Parent = args[2];
                        var element2 = new UIElement {Element = element};
                        element2.Draw(player);
                        collection.Elements.Add(element2);
                    }

                    if (args.Length > 3)
                    {
                        var element = collection.FindElement(args[1]);
                        if (element == null)
                        {
                            player.ChatMessage(GetMsg("Element Not Found", id));
                            return;
                        }
                        
                        for (var i = 2; i < args.Length; i++)
                        {
                            switch (args[i])
                            {
                                case "transform":
                                {
                                    element.Element.Components.Add(new CuiRectTransformComponent());
                                    break;
                                }

                                case "button":
                                {
                                    element.Element.Components.Add(new CuiButtonComponent());
                                    break;
                                }

                                case "image":
                                {
                                    element.Element.Components.Add(new CuiImageComponent());
                                    break;
                                }

                                case "raw":
                                {
                                    element.Element.Components.Add(new CuiRawImageComponent());
                                    break;
                                }

                                case "text":
                                {
                                    element.Element.Components.Add(new CuiTextComponent());
                                    break;
                                }

                                case "input":
                                {
                                    element.Element.Components.Add(new CuiInputFieldComponent());
                                    break;
                                }

                                case "outline":
                                {
                                    element.Element.Components.Add(new CuiOutlineComponent());
                                    break;
                                }

                                case "cursor":
                                {
                                    element.Element.Components.Add(new CuiNeedsCursorComponent());
                                    break;
                                }
                            }
                        }
                    }
                    
                    break;
                }
                
                #endregion

                #region Remove

                case "remove":
                {
                    UICollection collection;
                    if (!_active.TryGetValue(player, out collection))
                    {
                        player.ChatMessage(GetMsg("Collection Not Found", id));
                        return;
                    }

                    if (args.Length < 2)
                    {
                        player.ChatMessage(GetMsg("Help: Remove", id));
                        return;
                    }

                    var element = collection.FindElement(args[1]);
                    if (element == null)
                    {
                        player.ChatMessage(GetMsg("Element Not Found", id));
                        return;
                    }

                    if (args.Length == 2)
                    {
                        element.Destroy(player);
                        collection.Elements.Remove(element);
                    }

                    if (args.Length > 2)
                    {
                        for (var i = 2; i < args.Length; i++)
                        {
                            switch (args[i])
                            {
                                case "transform":
                                {
                                    element.Element.Components.Remove(new CuiRectTransformComponent());
                                    break;
                                }

                                case "button":
                                {
                                    element.Element.Components.Remove(new CuiButtonComponent());
                                    break;
                                }

                                case "image":
                                {
                                    element.Element.Components.Remove(new CuiImageComponent());
                                    break;
                                }

                                case "raw":
                                {
                                    element.Element.Components.Remove(new CuiRawImageComponent());
                                    break;
                                }

                                case "text":
                                {
                                    element.Element.Components.Remove(new CuiTextComponent());
                                    break;
                                }

                                case "input":
                                {
                                    element.Element.Components.Remove(new CuiInputFieldComponent());
                                    break;
                                }

                                case "outline":
                                {
                                    element.Element.Components.Remove(new CuiOutlineComponent());
                                    break;
                                }

                                case "cursor":
                                {
                                    element.Element.Components.Remove(new CuiNeedsCursorComponent());
                                    break;
                                }
                            }
                        }
                        
                        element.Draw(player);
                    }

                    break;
                }

                #endregion

                case "set":
                {
                    UICollection collection;
                    if (!_active.TryGetValue(player, out collection))
                    {
                        player.ChatMessage(GetMsg("Collection Not Found", id));
                        return;
                    }

                    if (args.Length < 5)
                    {
                        player.ChatMessage(GetMsg("Help: Set", id));
                        return;
                    }

                    var element = collection.FindElement(args[1]);
                    if (element == null)
                    {
                        player.ChatMessage(GetMsg("Element Not Found", id));
                        return;
                    }
                    
                    element.ChangeValue(args);
                    break;
                }
            }
        }
        
        #endregion
        
        #region UI

        private class UICollection
        {
            public string Name;
            public List<UIElement> Elements = new List<UIElement>();

            public static UICollection FindCollection(string name)
            {
                var dataCount = _data.Count;
                for (var i = 0; i < dataCount; i++)
                {
                    var collection = _data[i];
                    if (collection.Name == name)
                        return collection;
                }

                return null;
            }

            public UIElement FindElement(string name)
            {
                var elementsCount = Elements.Count;
                for (var i = 0; i < elementsCount; i++)
                {
                    var element = Elements[i];
                    if (element.Element.Name == name)
                        return element;
                }

                return null;
            }

            public void Destroy(BasePlayer player)
            {
                var elementsCount = Elements.Count;
                for (var i = 0; i < elementsCount; i++)
                {
                    var element = Elements[i];
                    element.Destroy(player);
                }
            }

            public void Draw(BasePlayer player)
            {
                var elementsCount = Elements.Count;
                for (var i = 0; i < elementsCount; i++)
                {
                    var element = Elements[i];
                    element.Draw(player);
                }
            }
        }

        private class UIElement
        {
            public CuiElement Element;

            public string Json() =>
                JsonConvert.SerializeObject(Element, 0, new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore
                }).Replace("\\n", "\n");

            public void Destroy(BasePlayer player)
            {
                if (player == null || player.net == null)
                    return;
                
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo()
                {
                    connection = player.net.connection
                }, null, "DestroyUI", Element.Name);
            }

            public void Draw(BasePlayer player)
            {
                if (player == null || player.net == null)
                    return;
                
                Destroy(player);
                
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo()
                {
                    connection = player.net.connection
                }, null, "AddUI", Json());
            }

            public void ChangeValue(string[] args)
            {
                var componentString = args[2];
                var fieldString = args[3];

                switch (componentString)
                {
                    case "transform":
                    {
                        var component = Element.Components.Find(x => x.Type == "RectTransform") as CuiRectTransformComponent;
                        if (component == null) return;
                        
                        
                        break;
                    }

                    case "button":
                    {
                        var component = Element.Components.Find(x => x.Type == "UnityEngine.UI.Button") as CuiButtonComponent;
                        if (component == null) return;


                        break;
                    }

                    case "image":
                    {
                        var component = Element.Components.Find(x => x.Type == "UnityEngine.UI.Image") as CuiImageComponent;
                        if (component == null) return;


                        break;
                    }

                    case "raw":
                    {
                        var component = Element.Components.Find(x => x.Type == "UnityEngine.UI.RawImage") as CuiRawImageComponent;
                        if (component == null) return;


                        break;
                    }

                    case "text":
                    {
                        var component = Element.Components.Find(x => x.Type == "UnityEngine.UI.Text") as CuiTextComponent;
                        if (component == null) return;


                        break;
                    }

                    case "input":
                    {
                        var component = Element.Components.Find(x => x.Type == "UnityEngine.UI.InputField") as CuiInputFieldComponent;
                        if (component == null) return;


                        break;
                    }

                    case "outline":
                    {
                        var component = Element.Components.Find(x => x.Type == "UnityEngine.UI.Outline") as CuiOutlineComponent;
                        if (component == null) return;


                        break;
                    }

                    case "cursor":
                    {
                        var component = Element.Components.Find(x => x.Type == "NeedsCursor") as CuiNeedsCursorComponent;
                        if (component == null) return;


                        break;
                    }
                }
            }
        }

        #endregion
        
        #region Helpers

        private string GetMsg(string key, string userId) => lang.GetMessage(key, this, userId);

        #endregion
    }
}