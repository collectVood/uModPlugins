using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("RadPlus", "Iv Misticos", "1.0.0")] // TODO: Rename like Apocalypsys ? ?
    [Description("Adds cool radiation things")]
    class RadPlus : RustPlugin
    {
        #region Variables

        public static readonly int EntMask = LayerMask.GetMask("Construction", "Deployed");
        public static bool RadiationEnabled = false;
        public static Random Random = new Random();
        
        #endregion
        
        #region Configuration

        private static Configuration _config = new Configuration();
        
        public class Configuration
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
            
            [JsonProperty(PropertyName = "Min Time Between Rad Rains")]
            public string RadTimeBetweenMin = "15m";
            
            [JsonProperty(PropertyName = "Max Time Between Rad Rains")]
            public string RadTimeBetweenMax = "30m";

            [JsonProperty(PropertyName = "Min Rad Rains Duration")]
            public string RadTimeDurationMin = "5m";
            
            [JsonProperty(PropertyName = "Max Rad Rains Duration")]
            public string RadTimeDurationMax = "15m";

            [JsonIgnore] public int ParsedRadTimeBetweenMin;
            [JsonIgnore] public int ParsedRadTimeBetweenMax;
            [JsonIgnore] public int ParsedRadTimeDurationMin;
            [JsonIgnore] public int ParsedRadTimeDurationMax;
            
            [JsonProperty(PropertyName = "Radiation Amount")]
            public float Radiation = 0.75f;
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
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
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

        protected override void LoadDefaultMessages()
        {
            
        }

        private void OnServerInitialized()
        {
            RadiationEnabled = false;

            if (!ConvertToSeconds(_config.RadTimeBetweenMin, out _config.ParsedRadTimeBetweenMin))
            {
                PrintError($"Unable to convert \"{_config.RadTimeBetweenMin}\" to seconds!");
                Interface.GetMod().UnloadPlugin(Name);
                return;
            }

            if (!ConvertToSeconds(_config.RadTimeBetweenMax, out _config.ParsedRadTimeBetweenMax))
            {
                PrintError($"Unable to convert \"{_config.RadTimeBetweenMax}\" to seconds!");
                Interface.GetMod().UnloadPlugin(Name);
                return;
            }

            if (!ConvertToSeconds(_config.RadTimeDurationMin, out _config.ParsedRadTimeDurationMin))
            {
                PrintError($"Unable to convert \"{_config.RadTimeDurationMin}\" to seconds!");
                Interface.GetMod().UnloadPlugin(Name);
                return;
            }

            if (!ConvertToSeconds(_config.RadTimeDurationMax, out _config.ParsedRadTimeDurationMax))
            {
                PrintError($"Unable to convert \"{_config.RadTimeDurationMax}\" to seconds!");
                Interface.GetMod().UnloadPlugin(Name);
                return;
            }
            
            var players = BasePlayer.activePlayerList;
            var playersCount = players.Count;
            for (var i = 0; i < playersCount; i++)
            {
                OnPlayerInit(players[i]);
            }

            RadiationTimer();
        }

        private void OnPlayerInit(BasePlayer player)
        {
            player.gameObject.AddComponent<RadPlayerController>();
        }

        private void Unload()
        {
            RadiationEnabled = false;
        }
        
        #endregion
        
        #region Helpers

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogInfo($"[RadPlus DEBUG]: {message}");
        }

        private void RadiationTimer() =>
            timer.Once(Random.Next(_config.ParsedRadTimeBetweenMin, _config.ParsedRadTimeBetweenMax), RadiationStart);

        private void RadiationStart()
        {
            RadiationEnabled = true;

            foreach (var p in BasePlayer.activePlayerList)
            {
                p.ChatMessage("Начался радиационный дождь!");
            }

            PrintDebug("Enabled Rad Rain");
            timer.Once(Random.Next(_config.ParsedRadTimeDurationMin, _config.ParsedRadTimeDurationMax), RadiationStop);
        }

        private void RadiationStop()
        {
            RadiationEnabled = false;

            Publish("Радиационный дождь закончился!");
            
            PrintDebug("Disabled Rad Rain");
            RadiationTimer();
        }

        private void Publish(string s)
        {
            var players = BasePlayer.activePlayerList;
            var playersCount = players.Count;
            for (var i = 0; i < playersCount; i++)
            {
                players[i].ChatMessage(s);
            }
        }
        
        #endregion
        
        #region Controllers

        public class RadPlayerController : MonoBehaviour
        {
            public BasePlayer player;
            public uint TimeSinceInDanger = 0;

            public void Awake()
            {
                player = gameObject.GetComponent<BasePlayer>();
                InvokeRepeating(nameof(GiveRadiation), 1f, 1f);
            }

            public bool UnderEntity() => Physics.Raycast(player.eyes.transform.position, Vector3.up, EntMask);

            public void GiveRadiation()
            {
                if (RadiationEnabled && !UnderEntity())
                {
                    TimeSinceInDanger++;
                    player.metabolism.radiation_poison.SetValue(_config.Radiation * TimeSinceInDanger);
                    player.metabolism.radiation_level.SetValue(_config.Radiation * TimeSinceInDanger);
                }
                else
                {
                    TimeSinceInDanger = 0;
                }

//                player.metabolism.poison.Increase(_config.Radiation * TimeSinceInDanger);
            }
        }
        
        #endregion
        
        #region Parsers
        
        private static readonly Regex RegexStringTime = new Regex(@"(\d+)([dhms])", RegexOptions.Compiled);
        private static bool ConvertToSeconds(string time, out int seconds)
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
                        seconds += int.Parse(match.Groups[1].Value) * 24 * 60 * 60;
                        break;
                    }
                    case "h":
                    {
                        seconds += int.Parse(match.Groups[1].Value) * 60 * 60;
                        break;
                    }
                    case "m":
                    {
                        seconds += int.Parse(match.Groups[1].Value) * 60;
                        break;
                    }
                    case "s":
                    {
                        seconds += int.Parse(match.Groups[1].Value);
                        break;
                    }
                }
            }
            return true;
        }
        
        #endregion
    }
}