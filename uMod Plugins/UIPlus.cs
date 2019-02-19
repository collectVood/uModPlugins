using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("UI Plus", "Iv Misticos", "2.0.0")]
    [Description("Adds various custom elements to the user interface")]
    class UIPlus : RustPlugin
    {
        #region Variables

        private static UIPlus _ins;

        private DataFile _data = new DataFile();

        #endregion

        #region Work with Data

        public class DataFile
        {
            public Dictionary<ulong, bool> IsEnabled = new Dictionary<ulong, bool>();
        }

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.GetMod().DataFileSystem.ReadObject<DataFile>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new DataFile();
        }

        #endregion

        #region Configuration

        private static Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;

            [JsonProperty(PropertyName = "Clock Settings")]
            public ConfigurationClock Clock = new ConfigurationClock();

            [JsonProperty(PropertyName = "Active Players Settings")]
            public ConfigurationActivePlayers ActivePlayers = new ConfigurationActivePlayers();

            [JsonProperty(PropertyName = "Sleeping Players Settings")]
            public ConfigurationSleepingPlayers SleepingPlayers = new ConfigurationSleepingPlayers();

            [JsonProperty(PropertyName = "Helicopter Settings")]
            public ConfigurationHelicopter Helicopter = new ConfigurationHelicopter();

            [JsonProperty(PropertyName = "Chinook47 Settings")]
            public ConfigurationChinook47 Chinook47 = new ConfigurationChinook47();

            [JsonProperty(PropertyName = "Plane Settings")]
            public ConfigurationPlane Plane = new ConfigurationPlane();

            [JsonProperty(PropertyName = "Tank Settings")]
            public ConfigurationTank Tank = new ConfigurationTank();

            [JsonProperty(PropertyName = "Cargo Ship Settings")]
            public ConfigurationCargoShip CargoShip = new ConfigurationCargoShip();

            [JsonProperty(PropertyName = "Auto Message Settings")]
            public ConfigurationAutoMessages AutoMessages = new ConfigurationAutoMessages();
        }

        public class ConfigurationClock
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;

            [JsonProperty(PropertyName = "24 Hour Format")]
            public bool ClockFormat24 = true;

            [JsonProperty(PropertyName = "Show Seconds")]
            public bool ClockShowSeconds = false;

            [JsonProperty(PropertyName = "Clock Update Frequency")]
            public float ClockUpdate = 3f;
        }

        public class ConfigurationActivePlayers
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
        }

        public class ConfigurationSleepingPlayers
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
        }

        public class ConfigurationHelicopter
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
        }

        public class ConfigurationChinook47
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
        }

        public class ConfigurationPlane
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
        }

        public class ConfigurationTank
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
        }

        public class ConfigurationCargoShip
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
        }

        public class ConfigurationAutoMessages
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;

            [JsonProperty(PropertyName = "Auto Message Groups",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConfigurationAutoMessageGroup> Groups = new List<ConfigurationAutoMessageGroup>
                {new ConfigurationAutoMessageGroup()};

            public static ConfigurationAutoMessage Find(string id)
            {
                for (var i = 0; i < _config.AutoMessages.Groups.Count; i++)
                {
                    var group = _config.AutoMessages.Groups[i];
                    if (group.Current != null && (string.IsNullOrEmpty(group.Permission) ||
                                                  _ins.permission.UserHasPermission(id, group.Permission)))
                        return group.Current;

                }

                return null;
            }
        }

        public class ConfigurationAutoMessageGroup
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "";

            [JsonProperty(PropertyName = "Frequency")]
            public string Frequency = "3m";

            [JsonIgnore] public uint ParsedFrequency;

            [JsonIgnore] public ConfigurationAutoMessage Current;

            [JsonProperty(PropertyName = "Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConfigurationAutoMessage> Messages = new List<ConfigurationAutoMessage>
                {new ConfigurationAutoMessage()};
        }

        public class ConfigurationAutoMessage
        {
            [JsonProperty(PropertyName = "Message")]
            public string Message = "Test message";

            [JsonProperty(PropertyName = "Text Size")]
            public int TextSize = 16;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch (Exception e)
            {
                Config.WriteObject(_config, false, $"{Interface.GetMod().ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Hooks

        // ReSharper disable once UnusedMember.Local
        private void OnServerInitialized()
        {
            _ins = this;
            
            // Loading configuration
            
            LoadConfig();

            // Parsing config variables, ..
            
            var groupsCount = _config.AutoMessages.Groups.Count;
            for (var i = 0; i < groupsCount; i++)
            {
                var group = _config.AutoMessages.Groups[i];

                if (!ConvertToSeconds(group.Frequency, out group.ParsedFrequency))
                {
                    PrintError($"Unable to convert \"{group.Frequency}\" to seconds!");
                    Interface.GetMod().UnloadPlugin(Name);
                    return;
                }

                if (!string.IsNullOrEmpty(group.Permission))
                    permission.RegisterPermission(group.Permission, this);
            }

            // Initializing GUI
            
            // TODO

            LoadData();

            var players = BasePlayer.activePlayerList;
            var playersCount = players.Count;
            for (var i = 0; i < playersCount; i++)
            {
                OnPlayerInit(players[i]);
            }
        }

        // ReSharper disable once UnusedMember.Local
        private void Unload()
        {
            var players = BasePlayer.activePlayerList;
            var playersCount = players.Count;
            for (var i = 0; i < playersCount; i++)
            {
                OnPlayerDisconnected(players[i], string.Empty);
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            _data.IsEnabled.TryAdd(player.userID, true);

            if (_data.IsEnabled[player.userID])
                player.gameObject.AddComponent<UIPlayerController>();
        }

        // ReSharper disable once UnusedMember.Local
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_data.IsEnabled[player.userID])
                player.gameObject.GetComponent<UIPlayerController>().OnDestroy();
        }

        #endregion
        
        #region Commands

        private void CommandChatToggle(BasePlayer player, string command, string[] args)
        {
            var newEntry = _data.IsEnabled[player.userID] = !_data.IsEnabled[player.userID];
            // TODO: Message
            // TODO: Register command
        }
        
        #endregion

        #region GUI



        #endregion

        #region Controllers

        public class UIPlayerController : FacepunchBehaviour
        {
            public BasePlayer player;

            private void Awake()
            {
                player = gameObject.GetComponent<BasePlayer>();
                
                InvokeRepeating(UpdateUIClock, _config.Clock.ClockUpdate, _config.Clock.ClockUpdate);
            }

            public void UpdateUI()
            {
                UpdateUIClock();
                UpdateUIActivePlayers();
                UpdateUISleepingPlayers();
                UpdateUIHelicopter();
                UpdateUIChinook47();
                UpdateUIPlane();
                UpdateUITank();
                UpdateUICargoShip();
                UpdateUIAutoMessages();
            }

            public void UpdateUIClock()
            {
                // TODO
            }

            public void UpdateUIActivePlayers()
            {
                // TODO
            }

            public void UpdateUISleepingPlayers()
            {
                // TODO
            }

            public void UpdateUIHelicopter()
            {
                // TODO
            }

            public void UpdateUIChinook47()
            {
                // TODO
            }

            public void UpdateUIPlane()
            {
                // TODO
            }

            public void UpdateUITank()
            {
                // TODO
            }

            public void UpdateUICargoShip()
            {
                // TODO
            }

            public void UpdateUIAutoMessages()
            {
                // TODO
            }

            public void DestroyUI()
            {
                // TODO
            }

            public void OnDestroy()
            {
                CancelInvoke(UpdateUIClock);
                DestroyUI();
            }
        }

        #endregion

        #region Helpers

        private void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }

        #endregion

        #region Parsers

        private static readonly Regex RegexStringTime = new Regex(@"(\d+)([dhms])", RegexOptions.Compiled);

        private static bool ConvertToSeconds(string time, out uint seconds)
        {
            seconds = 0;
            if (time == "0" || string.IsNullOrEmpty(time)) return true;
            var matches = RegexStringTime.Matches(time);
            if (matches.Count == 0) return false;
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (match.Groups[2].Value)
                {
                    case "d":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 24 * 60 * 60;
                        break;
                    }
                    case "h":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60 * 60;
                        break;
                    }
                    case "m":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60;
                        break;
                    }
                    case "s":
                    {
                        seconds += uint.Parse(match.Groups[1].Value);
                        break;
                    }
                }
            }

            return true;
        }

        #endregion
    }
}