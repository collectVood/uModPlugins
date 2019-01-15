using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Shared Auth", "Iv Misticos", "1.0.0")]
    [Description("Make sharing auth better")]
    public class SharedAuth : CovalencePlugin
    {
        #region Variables
        
        private Dictionary<ulong, PlayerData> _data = new Dictionary<ulong, PlayerData>();

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
                _data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public bool Enabled = false;

            public bool AutoTeamAuth = false;
            public bool AllowTeamUse = true;
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

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsAdmin || iPlayer.HasPermission(MasterPerm))
            {
                _holders.AddMaster(player.userID.ToString());
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.IsAdmin || iPlayer.HasPermission(MasterPerm))
            {
                _holders.RemoveMaster(player.userID.ToString());
            }
        }

        private bool CanUseLockedEntity(BasePlayer player, BaseLock door)
        {
            var iPlayer = covalence.Players.FindPlayerById(player.userID.ToString());
            var canUse = player.IsAdmin && _holders.IsAKeyMaster(player.userID.ToString())
                          || iPlayer.HasPermission(MasterPerm) && _holders.IsAKeyMaster(player.userID.ToString())
                          || new DoorAuthorizer(door, player).CanOpen();
            
            return canUse;
        }
        
        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            return null;
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
            PlayerData data;
            return _data.TryGetValue(id, out data) ? data : null;
        }

        #endregion
    }
}