using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.DiscordObjects;
using Oxide.Ext.Discord.Exceptions;

namespace Oxide.Plugins
{
    [Info("Discord Auth", "Iv Misticos", "1.0.0")]
    [Description("Discord Auth with API")]
    class DiscordAuth : CovalencePlugin
    {
        #region Variables
        
#pragma warning disable 649
        [DiscordClient] private DiscordClient _client;
#pragma warning restore 649

        private static List<KeyInfo> _keys = new List<KeyInfo>();

        private static List<PlayerData> _data;
        
        #endregion
        
        #region Work with Data

        // Temporary
        private class KeyInfo
        {
            public string UserId;
            public string Key;

            public static KeyInfo FindByID(string userId)
            {
                for (var i = 0; i < _keys.Count; i++)
                {
                    if (_keys[i].UserId == userId)
                        return _keys[i];
                }

                return null;
            }

            public static KeyInfo FindByKey(string key)
            {
                for (var i = 0; i < _keys.Count; i++)
                {
                    if (_keys[i].Key == key)
                        return _keys[i];
                }

                return null;
            }
        }

        // Non-Temporary
        private class PlayerData
        {
            public string GameId;
            public string DiscordId;

            public static PlayerData FindByGame(string id)
            {
                for (var i = 0; i < _data.Count; i++)
                {
                    if (_data[i].GameId == id)
                        return _data[i];
                }

                return null;
            }

            public static PlayerData FindByDiscord(string id)
            {
                for (var i = 0; i < _data.Count; i++)
                {
                    if (_data[i].DiscordId == id)
                        return _data[i];
                }

                return null;
            }
        }

        private void SaveData()
        {
            switch (_config.Storage.Type)
            {
                // TODO
            }
        }

        private void LoadData()
        {
            // TODO
        }

        private void SaveDataFiles() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadDataFiles()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<List<PlayerData>>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new List<PlayerData>();
        }
        
        #endregion
        
        #region Configuration
        
        private static Configuration _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Code Length")]
            public int CodeLength = 10;
            
            [JsonProperty(PropertyName = "Command")]
            public string Command = "key";
            
            [JsonProperty(PropertyName = "Discord API Token")]
            public string APIToken = "";
            
            [JsonProperty(PropertyName = "Game Playing")]
            public string Game = "Auth Codes";
            
            [JsonProperty(PropertyName = "Storage")]
            public ConfigurationDataStorage Storage = new ConfigurationDataStorage();
        }

        private class ConfigurationDataStorage
        {
            [JsonProperty(PropertyName = "Data Storage Type (MySQL / SQLite / Files)")]
            public string Type = "Files";
            
            [JsonProperty(PropertyName = "MySQL Settings")]
            public ConfigurationDataStorageMySQL MySQL = new ConfigurationDataStorageMySQL();
        }

        private class ConfigurationDataStorageMySQL
        {
            [JsonProperty(PropertyName = "Host")]
            public string Host = "127.0.0.1";
            
            [JsonProperty(PropertyName = "Port")]
            public int Port = 3306;
            
            [JsonProperty(PropertyName = "Database")]
            public string Database = "database";
            
            [JsonProperty(PropertyName = "Table")]
            public string Table = "table";
            
            [JsonProperty(PropertyName = "Username")]
            public string Username = "username";
            
            [JsonProperty(PropertyName = "Password")]
            public string Password = "password";
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
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Code Message", "Your code: {code}." },
                { "Connected", "Your Discord account is now connected to your game account." },
                { "Incorrect Code", "You have entered an incorrect code." },
                { "Rewrite Discord Connection", "You already had a connection on this discord account with a game account. It will be removed." },
                { "Rewrite Game Connection", "You already had a connection on this game account with a discord account. It will be removed." },
            }, this);
        }

        private void Loaded()
        {
            LoadData();
            
            try
            {
                Discord.CreateClient(this, _config.APIToken);
            }
            catch (APIKeyException)
            {
                PrintWarning("Please, enter a correct API token");
                Interface.Oxide.UnloadPlugin(Name);
            }
            
            AddCovalenceCommand(_config.Command, "CommandRun");

            timer.Once(5f, () =>
            {
                _client?.UpdateStatus(new Presence
                {
                    AFK = false,
                    Game = new Ext.Discord.DiscordObjects.Game
                    {
                        Name = _config.Game,
                        Type = ActivityType.Game
                    }
                });
            });
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            SaveData();
            Discord.CloseClient(_client);
        }
        
        #region Discord Hooks
        
        // Called when a message is created on the Discord server
        private void Discord_MessageCreate(Message message)
        {
            // No self-check
            if (message.author.bot == true)
                return;

            Channel.GetChannel(_client, message.channel_id, channel =>
            {
                // Only DM
                if (channel.type != ChannelType.DM)
                    return;

                // Trying to find a key
                var found = KeyInfo.FindByKey(message.content);
                if (found == null)
                {
                    // Nope
                    channel.CreateMessage(_client, GetMsg("Incorrect Code"));
                    return;
                }

                // Yeah
                channel.CreateMessage(_client, GetMsg("Connected", found.UserId));
                var data1 = PlayerData.FindByGame(found.UserId);
                var data2 = PlayerData.FindByDiscord(message.author.id);

                if (data1 != null)
                {
                    channel.CreateMessage(_client, GetMsg("Rewrite Game Connection", found.UserId));
                    _data.Remove(data1);
                }

                if (data2 != null)
                {
                    channel.CreateMessage(_client, GetMsg("Rewrite Discord Connection", found.UserId));
                    _data.Remove(data2);
                }

                _data.Add(new PlayerData
                {
                    GameId = found.UserId,
                    DiscordId = message.author.id
                });

                _keys.Remove(found);
            });
        }

        #endregion
        
        #endregion
        
        #region Commands

        private bool CommandRun(IPlayer caller, string command, string[] args)
        {
            var found = KeyInfo.FindByID(caller.Id);
            var code = GenerateCode();
            if (found != null)
                found.Key = code;
            else
            {
                _keys.Add(new KeyInfo
                {
                    UserId = caller.Id,
                    Key = code
                });
            }
            
            caller.Reply(GetMsg("Code Message", caller.Id).Replace("{code}", code));
            return true;
        }
        
        #endregion
        
        #region Helpers

        private string GenerateCode()
        {
            var builder = new StringBuilder();
            while (builder.Length < _config.CodeLength)
            {
                builder.Append(Guid.NewGuid().ToString().Replace("-", ""));
            }

            builder.Length = _config.CodeLength;
            return builder.ToString();
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
        
        #region API

        private string GetDiscordByGameID(string id) => PlayerData.FindByGame(id)?.DiscordId;

        private string GetGameByDiscordID(string id) => PlayerData.FindByDiscord(id)?.GameId;

        #endregion
    }
}