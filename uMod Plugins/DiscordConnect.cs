using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.DiscordObjects;

namespace Oxide.Plugins
{
    [Info("Discord Connect", "Iv Misticos", "1.0.6")]
    [Description("Discord account connection with API")]
    public class DiscordConnect : CovalencePlugin
    {
        #region Variables
        
        [DiscordClient]
        private DiscordClient _client = null;

        private static List<KeyInfo> _keys = new List<KeyInfo>();

        private static List<PlayerData> _data = new List<PlayerData>();

        private static DiscordConnect _ins;

        private static Time _time = GetLibrary<Time>();
        
        #endregion

        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string Token = string.Empty;
            
            [JsonProperty(PropertyName = "Channel ID For Authentication Log")]
            public string AuthLogChannel = string.Empty;

            [JsonIgnore] public Channel ParsedAuthLogChannel;
            
            [JsonProperty(PropertyName = "Enable Bot Status")]
            public bool EnableStatus = true;
            
            [JsonProperty(PropertyName = "Bot Status")]
            public string Status = "Send me your code";
            
            [JsonProperty(PropertyName = "Group To Assign Once Connected")]
            public string GroupConnected = "discord-connected";
            
            [JsonProperty(PropertyName = "Group To Revoke Once Left")]
            public string GroupLeft = "discord-connected";
            
            [JsonProperty(PropertyName = "Delete Data On Discord Leave")]
            public bool DeleteData = false;
            
            [JsonProperty(PropertyName = "Allow Data Overwrite")]
            public bool OverwriteData = true;
            
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix = "Auth";
            
            [JsonProperty(PropertyName = "Auth Command")]
            public string Command = "auth";
            
            [JsonProperty(PropertyName = "Code Lifetime")]
            public string CodeLifetime = "15m";

            [JsonIgnore] public uint ParsedCodeLifetime;
            
            [JsonProperty(PropertyName = "Code Length")]
            public int CodeLength = 6;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        
        #endregion

        #region Work with Data

        // Temporary
        private class KeyInfo
        {
            public IPlayer Player;
            public string Key;

            public uint ValidUntil;

            public static KeyInfo FindByID(string userId)
            {
                for (var i = 0; i < _keys.Count; i++)
                {
                    if (_keys[i].Player.Id == userId)
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

            public void ExpireMessage()
            {
                Player.Reply(GetMsg("Code Expired", Player.Id), _config.Prefix);
            }

            public void ResetValidUntil()
            {
                ValidUntil = _time.GetUnixTimestamp() + _config.ParsedCodeLifetime;
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

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
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
        
        #region Hooks
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Code Generation", "Here is your code: {code}"},
                {"Code Expired", "Your code has expired!"},
                {"Authenticated", "Thank you for authenticating your account!"},
                {"Already Authenticated", "You have already authenticated your account, no need to do it again!"},
                {"Unable To Find Code", "Sorry, we couldn't find your code. Try to authenticate again."},
                {"Log", "{discordName} ({discordId}) has authenticated his account to in-game {gameName} ({gameId})"},
                {"Discord Overwrite", "Your old connection with this Discord account will be overwritten!"},
                {"In-Game Overwrite", "Your old connection with this in-game account will be overwritten!"},
                {"Authentication Denied By Plugin", "Your authentication was denied by a plugin"}
            }, this);
        }

        private void Init()
        {
            _ins = this;
            
            var parsed = ConvertToSeconds(_config.CodeLifetime, out _config.ParsedCodeLifetime);
            if (!parsed)
            {
                PrintError("Please, specify correct code lifetime!");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            if (!string.IsNullOrEmpty(_config.Token))
            {
                Discord.CreateClient(this, _config.Token);

                if (_config.EnableStatus)
                {
                    _client.UpdateStatus(new Presence
                    {
                        Game = new Ext.Discord.DiscordObjects.Game
                        {
                            Name = _config.Status,
                            Type = ActivityType.Game
                        }
                    });
                }
            }

            if (!string.IsNullOrEmpty(_config.AuthLogChannel))
            {
                Channel.GetChannel(_client, _config.AuthLogChannel, channel =>
                    {
                        _config.ParsedAuthLogChannel = channel;
                    });
            }
            
            AddCovalenceCommand(_config.Command, "CommandAuth");
            
            LoadData();
            timer.Every(1f, DoExpiration);
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            SaveData();
            Discord.CloseClient(_client);
        }
        
        #region Discord Hooks
        
        // Called when a member leaves the Discord server
        private void Discord_MemberRemoved(GuildMember member)
        {
            // No user found
            var found = PlayerData.FindByDiscord(member.user.id);
            if (found == null)
                return;

            if (!string.IsNullOrEmpty(_config.GroupLeft))
            {
                var player = players.FindPlayerById(found.GameId);
                player.RemoveFromGroup(_config.GroupLeft);
            }

            Interface.Oxide.CallHook("OnDiscordAuthLeave", found.GameId, found.DiscordId);

            // Disabled in config
            if (!_config.DeleteData)
                return;

            _data.Remove(found);
        }
        
        // Called when a message is created on the Discord server
        private void Discord_MessageCreate(Message message)
        {
            // Bot-check
            if (message.author.bot == true)
                return;

            Channel.GetChannel(_client, message.channel_id, channel =>
            {
                // DM-check
                if (channel.type != ChannelType.DM)
                    return;

                // No code found
                var info = KeyInfo.FindByKey(message.content);
                if (info == null)
                {
                    channel.CreateMessage(_client, GetMsg("Unable To Find Code"));
                    return;
                }

                var canAuthenticate = Interface.Oxide.CallHook("CanDiscordAuthenticate", info.Key, message.author.id);
                if (canAuthenticate is bool && !(bool) canAuthenticate)
                {
                    channel.CreateMessage(_client, GetMsg("Authentication Denied By Plugin", info.Player.Id));
                    return;
                }

                // Already authenticated-check
                var data1 = PlayerData.FindByGame(info.Player.Id);
                var data2 = PlayerData.FindByDiscord(message.author.id);

                if (!_config.OverwriteData && (data1 != null || data2 != null))
                {
                    channel.CreateMessage(_client, GetMsg("Already Authenticated"));
                }
                else
                {
                    if (data1 != null)
                    {
                        channel.CreateMessage(_client, GetMsg("In-Game Overwrite", info.Player.Id));
                        Interface.Oxide.CallHook("OnDiscordAuthOverwrite", data1.GameId, info.Key, data1.DiscordId,
                            message.author.id);
                        
                        _data.Remove(data1);
                    }

                    if (data2 != null)
                    {
                        channel.CreateMessage(_client, GetMsg("Discord Overwrite", info.Player.Id));
                        Interface.Oxide.CallHook("OnDiscordAuthOverwrite", data2.GameId, info.Key, data2.DiscordId,
                            message.author.id);
                        
                        _data.Remove(data2);
                    }
                }

                // Adds to data file
                _data.Add(new PlayerData
                {
                    DiscordId = message.author.id,
                    GameId = info.Key
                });

                channel.CreateMessage(_client, GetMsg("Authenticated", info.Player.Id));

                _config.ParsedAuthLogChannel?.CreateMessage(_client,
                    new StringBuilder(GetMsg("Log")).Replace("{discordName}", message.author.username)
                        .Replace("{discordId}", message.author.id).Replace("{gameId}", info.Player.Id)
                        .Replace("{gameName}", info.Player.Name).ToString());

                if (!string.IsNullOrEmpty(_config.GroupConnected))
                {
                    info.Player.AddToGroup(_config.GroupConnected);
                }
                
                Interface.Oxide.CallHook("OnDiscordAuthenticate", info.Key, message.author.id);
            });
        }

        #endregion
        
        #endregion

        #region Commands
        
        private void CommandAuth(IPlayer player, string command, string[] args)
        {
            // Already authenticated-check
            if (PlayerData.FindByGame(player.Id) != null)
            {
                player.Reply(GetMsg("Already Authenticated", player.Id), _config.Prefix);
                return;
            }

            var info = KeyInfo.FindByID(player.Id);
            var code = GenerateCode();
            if (info == null)
            {
                info = new KeyInfo();
                _keys.Add(info);
            }

            info.Key = code;
            info.Player = player;
            info.ResetValidUntil();
            
            player.Reply(GetMsg("Code Generation", player.Id).Replace("{code}", code));
        }
        
        #endregion

        #region API

        private string GetDiscordOf(string id) => PlayerData.FindByGame(id)?.DiscordId;
        
        private string GetGameOf(string id) => PlayerData.FindByDiscord(id)?.GameId;

        #endregion
        
        #region Code Expiration

        private void DoExpiration()
        {
            var time = _time.GetUnixTimestamp();
            for (var i = _keys.Count - 1; i >= 0; i--)
            {
                var key = _keys[i];
                if (key.ValidUntil > time) continue;

                key.ExpireMessage();
                _keys.RemoveAt(i);
            }
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

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);
        
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
