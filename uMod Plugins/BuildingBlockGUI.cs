using System;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingBlockGUI", "Iv Misticos", "2.0.0")]
    [Description("Displays GUI to player when he enters or leaves building block without need of Planner")]
    public class BuildingBlockGUI : RustPlugin
    {
        #region Config

        private List<ulong> _activeUI = new List<ulong>();
        private bool _configChanged;
        private float _configTimerSeconds;
        private bool _configUseTimer;
        private bool _configUseImage;
        private string _configImageUrl;
        private bool _configUseGameTips;
        private string _configAnchorMin;
        private string _configAnchorMax;
        private string _configUIColor;
        private string _configUITextColor;

        private Timer _timer;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("No configuration file found, generating...");
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            _configUseTimer = Convert.ToBoolean(GetConfig("useTimer", true));
            _configUseImage = Convert.ToBoolean(GetConfig("useImage", false));
            _configImageUrl = Convert.ToString(GetConfig("ImageURL", "http://oxidemod.org/data/resource_icons/2/2713.jpg?1512759786"));
            _configUseGameTips = Convert.ToBoolean(GetConfig("UseGameTips", false));
            _configTimerSeconds = Convert.ToSingle(GetConfig("timerSeconds", 0.5f));
            _configAnchorMin = Convert.ToString(GetConfig("AnchorMin", "0.35 0.11"));
            _configAnchorMax = Convert.ToString(GetConfig("AnchorMax", "0.63 0.14"));
            _configUIColor = Convert.ToString(GetConfig("UIColor", "1 0 0 0.15"));
            _configUITextColor = Convert.ToString(GetConfig("UITextColor", "1 1 1"));
            if (_configChanged)
            {
                SaveConfig();
                _configChanged = false;
            }
        }

        private object GetConfig(string dataValue, object defaultValue)
        {
            var value = Config[dataValue];
            if (value == null)
            {
                value = defaultValue;
                Config[dataValue] = value;
                _configChanged = true;
            }
            return value;
        }

        #endregion

        #region Messages

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"text", "BUILDING BLOCKED" }

            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"text", "СТРОИТЕЛЬСТВО ЗАПРЕЩЕНО" }
            }, this, "ru");
        }
        private string Msg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        
        #endregion

        #region Hooks

        private void Init()
        {
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            if (_configUseTimer)
            {
                _timer = timer.Repeat(_configTimerSeconds, 0, PluginTimerTick);
            }
        }

        private void Unload()
        {
            _timer?.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyUI(player);
        }

        #endregion

        #region UI

        private void DestroyUI(BasePlayer player)
        {
            if (!_activeUI.Contains(player.userID)) return;
            if (_configUseGameTips) player.SendConsoleCommand("gametip.hidegametip");
            else CuiHelper.DestroyUi(player, "BuildingBlockGUI");
            _activeUI.Remove(player.userID);
        }

        private void CreateUI(BasePlayer player)
        {
            if (_activeUI.Contains(player.userID)) return;
            if (_configUseGameTips)
            {
                player.SendConsoleCommand("gametip.hidegametip");
                player.SendConsoleCommand("gametip.showgametip", Msg("text", player));
                _activeUI.Add(player.userID);
                return;
            }
            DestroyUI(player);
            var container = new CuiElementContainer();
            if (_configUseImage)
            {
                var panel = container.Add(new CuiPanel
                {
                    Image = { Color = _configUIColor },
                    RectTransform = { AnchorMin = _configAnchorMin, AnchorMax = _configAnchorMax }
                }, "Hud", "BuildingBlockGUI");
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {
                            Url = _configImageUrl,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }
            else
            {
                var panel = container.Add(new CuiPanel
                {
                    Image = { Color = _configUIColor },
                    RectTransform = { AnchorMin = _configAnchorMin, AnchorMax = _configAnchorMax }
                }, "Hud", "BuildingBlockGUI");
                var element = new CuiElement
                {
                    Parent = panel,
                    Components = {
                        new CuiTextComponent { Text = Msg("text",player), FontSize = 15, Color = _configUITextColor, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0.0 0.0", AnchorMax = "1.0 1.0" }
                    }
                };
                container.Add(element);
            }

            CuiHelper.AddUi(player, container);
            _activeUI.Add(player.userID);
        }
        #endregion

        #region Helpers

        private void PluginTimerTick()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsBuildingBlocked())
                {
                    CreateUI(player);
                } else
                {
                    DestroyUI(player);
                }
            }
        }

        #endregion
    }
}