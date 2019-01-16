using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Shared Auth", "Iv Misticos", "1.0.0")]
    [Description("Make sharing auth better")]
    public class SharedAuth : CovalencePlugin
    {
        #region Variables
        
        private List<PlayerData> _data = new List<PlayerData>();

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

            public bool AutoTeamAuth = false;
            public bool AllowTeamUse = true;

            public bool IsAdmin()
            {
                var player = GetPlayer();
                return _ins.permission.UserHasPermission(ID.ToString(), PermissionAdmin)
                       || player == null || player.IsAdmin;
            }

            public bool IsTeamMember(ulong target)
            {
                var team = GetTeam();
                return team != null && team.members.IndexOf(target) != -1;
            }
        }
        
        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            _ins = this;

            LoadData();
            
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
        }

        private void Unload() => SaveData();

        private void OnServerSave() => SaveData();

        private object CanUseLockedEntity(BasePlayer player, BaseLock door)
        {
            var data = GetPlayerData(player.userID);
            if (!data.Enabled)
                return null;

            return data.IsAdmin() || data.AllowTeamUse && data.IsTeamMember(player.userID) ? (object) true : null;
        }
        
        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            var data = GetPlayerData(player.userID);
            if (!data.Enabled)
                return null;
            
            return data.IsAdmin() || data.AutoTeamAuth && data.IsTeamMember(player.userID) ? (object) true : null;
        }
        
        #endregion
        
        #region Commands

        private void CommandChatSharedAuth(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0].ToLower() == "help")
                {
                    PlayerResponder.NotifyUser(player, "Master Mode Toggle: /sd masterMode");
                }
                else if (args[0].ToLower() == "mastermode" || args[0].ToLower() == "mm")
                {
                    if (player.IsAdmin || player.HasPermission(MasterPerm))
                    {
                        if (_holders.HasMaster(player.Id))
                        {
                            _holders.ToggleMasterMode(player.Id);
                            if (_holders.IsAKeyMaster(player.Id))
                            {
                                PlayerResponder.NotifyUser(player, "Master Mode Enabled. You can now open all doors and chests.");
                            }
                            else
                            {
                                PlayerResponder.NotifyUser(player, "Master Mode Disabled. You can no longer open all doors and chests.");
                            }
                        }
                        else
                        {
                            _holders.AddMaster(player.Id);
                            _holders.GiveMasterKey(player.Id);
                            PlayerResponder.NotifyUser(player, "Master Mode Enabled. You can now open all doors and chests.");
                        }
                    }
                    else
                    {
                        PlayerResponder.NotifyUser(player, "Master Mode Not Available. You don't have permission to use this command.");
                    }
                }
            }
            else
            {
                PlayerResponder.NotifyUser(player, "Master Mode Toggle: /sd masterMode");
            }
        }
        
        #endregion
        
        #region Helpers

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private PlayerData GetPlayerData(ulong id)
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

        #endregion
    }
}