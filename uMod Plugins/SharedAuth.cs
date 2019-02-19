using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Shared Auth", "Iv Misticos", "1.0.0")]
    [Description("Make sharing auth better")]
    public class SharedAuth : RustPlugin
    {
        #region Variables

        // ReSharper disable once InconsistentNaming
        [PluginReference] private Plugin ClansReborn;
        [PluginReference] private Plugin Friends;
        
        private static List<PlayerData> _data = new List<PlayerData>();

        private const string PermissionUse = "sharedauth.use";
        private const string PermissionAdmin = "sharedauth.admin";

        private static SharedAuth _ins;
        
        #endregion
        
        #region Configuration
        
        private static Configuration _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Use Permission")]
            public bool UsePermission = true;
            
            [JsonProperty(PropertyName = "Command")]
            public string Command = "sauth";
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
        
        #region Data
        
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

        private class PlayerData
        {
            private BasePlayer GetPlayer() => BasePlayer.FindByID(ID) ?? BasePlayer.FindSleeping(ID);

            private RelationshipManager.PlayerTeam GetTeam()
            {
                foreach (var kvp in RelationshipManager.Instance.playerTeams)
                {
                    if (kvp.Value.members.Contains(ID))
                        return kvp.Value;
                }

                return null;
            }
            
            public ulong ID;
            public bool Enabled = true;

            // ReSharper disable once RedundantDefaultMemberInitializer
            public bool AllowTeamAuth = false;
            public bool AllowTeamUse = true;
            // ReSharper disable once RedundantDefaultMemberInitializer
            public bool AllowFriendsAuth = false;
            public bool AllowFriendsUse = true;
            // ReSharper disable once RedundantDefaultMemberInitializer
            public bool AllowClanAuth = false;
            // ReSharper disable once RedundantDefaultMemberInitializer
            public bool AllowClanUse = false;

            public bool IsAdmin()
            {
                var player = GetPlayer();
                return _ins.permission.UserHasPermission(ID.ToString(), PermissionAdmin)
                       || player == null && player.IsAdmin;
            }

            public bool IsTeamMember(ulong target)
            {
                var team = GetTeam();
                return team != null && team.members.IndexOf(target) != -1;
            }

            private bool IsFriendsAPIFriend(ulong target) => _ins.Friends.Call<bool>("IsFriend", ID, target);

            private bool IsClansRebornMember(ulong target) => _ins.ClansReborn.Call<string>("GetClanOf", ID) ==
                                                             _ins.ClansReborn.Call<string>("GetClanOf", target);

            public bool IsClanMember(ulong target)
            {
                return _ins.ClansReborn != null && IsClansRebornMember(target);
            }

            public bool IsFriend(ulong target)
            {
                return _ins.Friends != null && IsFriendsAPIFriend(target);
            }
            
            public static PlayerData GetPlayerData(ulong id)
            {
                for (var i = 0; i < _data.Count; i++)
                {
                    if (_data[i].ID == id)
                        return _data[i];
                }

                var data = new PlayerData {ID = id};
                _data.Add(data);
                return data;
            }
        }
        
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Help", "Shared Auth Help:\n" +
                          "enabled true/false - Enabled/disabled\n" +
                          "ta true/false - Auto team auth\n" +
                          "tu true/false - Allow team use without code\n" +
                          "fa true/false - Auto friends auth\n" +
                          "fu true/false - Allow friends use without code\n" +
                          "ca true/false - Auto clan auth\n" +
                          "cu true/false - Allow clan use without code" },
                { "Success", "Success." },
                { "No Permission", "You don't have enough permissions." },
            }, this);
        }

        private void OnServerInitialized()
        {
            _ins = this;

            LoadData();
            
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
            
            cmd.AddChatCommand(_config.Command, this, CommandChatSharedAuth);
        }

        private void Unload() => SaveData();

        private void OnServerSave() => SaveData();

        private object CanUseLockedEntity(BasePlayer player, BaseEntity door)
        {
            if (!CanUse(player.UserIDString))
                return null;

            var data = PlayerData.GetPlayerData(player.userID);
            if (!data.Enabled)
                return null;

            return data.IsAdmin() || data.AllowTeamUse && data.IsTeamMember(door.OwnerID) ||
                   data.AllowFriendsUse && data.IsFriend(door.OwnerID) ||
                   data.AllowClanUse && data.IsClanMember(door.OwnerID)
                ? (object) true
                : null;
        }
        
        private object OnCodeEntered(BaseEntity codeLock, BasePlayer player, string code)
        {
            if (!CanUse(player.UserIDString))
                return null;
            
            var data = PlayerData.GetPlayerData(player.userID);
            if (!data.Enabled)
                return null;

            return data.IsAdmin() || data.AllowTeamAuth && data.IsTeamMember(codeLock.OwnerID) ||
                   data.AllowFriendsAuth && data.IsFriend(codeLock.OwnerID) ||
                   data.AllowClanAuth && data.IsFriend(codeLock.OwnerID)
                ? (object) true
                : null;
        }
        
        #endregion
        
        #region Commands

        private void CommandChatSharedAuth(BasePlayer player, string command, string[] args)
        {
            if (!CanUse(player.UserIDString))
            {
                player.ChatMessage(GetMsg("No Permission", player.UserIDString));
                return;
            }
            
            if (args.Length < 2)
            {
                player.ChatMessage(GetMsg("Help", player.UserIDString));
                return;
            }

            var data = PlayerData.GetPlayerData(player.userID);
            var isTrue = args[1].Equals("true", StringComparison.CurrentCultureIgnoreCase) ||
                         args[1].Equals("yes", StringComparison.CurrentCultureIgnoreCase);
            
            switch (args[0])
            {
                case "enabled":
                {
                    data.Enabled = isTrue;
                    break;
                }

                case "ta":
                {
                    data.AllowTeamAuth = isTrue;
                    break;
                }

                case "tu":
                {
                    data.AllowTeamUse = isTrue;
                    break;
                }

                case "fa":
                {
                    data.AllowFriendsAuth = isTrue;
                    break;
                }

                case "fu":
                {
                    data.AllowFriendsUse = isTrue;
                    break;
                }

                case "ca":
                {
                    data.AllowClanAuth = isTrue;
                    break;
                }

                case "cu":
                {
                    data.AllowClanUse = isTrue;
                    break;
                }

                default:
                {
                    player.ChatMessage(GetMsg("Help", player.UserIDString));
                    return;
                }
            }
            
            player.ChatMessage(GetMsg("Success", player.UserIDString));
        }
        
        #endregion
        
        #region Helpers

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private bool CanUse(string id) => !_config.UsePermission || permission.UserHasPermission(id, PermissionUse);

        #endregion
    }
}