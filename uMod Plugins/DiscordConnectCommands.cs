using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Discord Connect Commands", "Iv Misticos", "1.0.1")]
    [Description("Execute commands on Discord Connect events")]
    class DiscordConnectCommands : CovalencePlugin
    {
        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands On Connect",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandsConnect = new List<string>
            {
                "echo {gameId} {discordId}"
            };

            [JsonProperty(PropertyName = "Commands On Overwrite",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandsOverwrite = new List<string>
            {
                "echo {oldGameId} {newGameId} {oldDiscordId} {newDiscordId}"
            };

            [JsonProperty(PropertyName = "Commands On Server Leave",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandsLeave = new List<string>
            {
                "echo {gameId} {discordId}"
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

        private void OnDiscordAuthenticate(string gameId, string discordId)
        {
            var builder = new StringBuilder();
            foreach (var command in _config.CommandsConnect)
            {
                builder.Length = 0;
                covalence.Server.Command(builder.Append(command).Replace("{gameId}", gameId)
                    .Replace("{discordId}", discordId).ToString());
            }
        }

        private void OnDiscordAuthOverwrite(string oldGameId, string newGameId, string oldDiscordId,
            string newDiscordId)
        {
            var builder = new StringBuilder();
            foreach (var command in _config.CommandsOverwrite)
            {
                builder.Length = 0;
                covalence.Server.Command(builder.Append(command).Replace("{oldGameId}", oldGameId)
                    .Replace("{newGameId}", newGameId).Replace("{oldDiscordId}", oldDiscordId)
                    .Replace("{newDiscordId}", newDiscordId).ToString());
            }
        }

        private void OnDiscordAuthLeave(string gameId, string discordId)
        {
            var builder = new StringBuilder();
            foreach (var command in _config.CommandsLeave)
            {
                builder.Length = 0;
                covalence.Server.Command(builder.Append(command).Replace("{gameId}", gameId)
                    .Replace("{discordId}", discordId).ToString());
            }
        }
        
        #endregion
    }
}