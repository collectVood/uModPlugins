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
        
        
        
        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            _ins = this;
            
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
        }

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

        private class PlayerResponder
        {
            private const string Prefix = "<color=#00ffffff>[</color><color=#ff0000ff>SharedDoors</color><color=#00ffffff>]</color>";

            public static void NotifyUser(IPlayer player, string message)
            {
                player.Message(Prefix + " " + message);
            }
        }

        private class DoorAuthorizer
        {
            public BaseLock BaseDoor { get; protected set; }
            public BasePlayer Player { get; protected set; }
            private ToolCupboardChecker _checker;
            private RustIoHandler _handler;

            public DoorAuthorizer(BaseLock door, BasePlayer player)
            {
                BaseDoor = door;
                Player = player;
                _checker = new ToolCupboardChecker(Player);
                _handler = new RustIoHandler(this);
            }

            public bool CanOpen()
            {
                var canUse = false;
                if (BaseDoor.IsLocked())
                {
                    if (BaseDoor is CodeLock)
                    {
                        var codeLock = (CodeLock)BaseDoor;
                        canUse = CanOpenCodeLock(codeLock, Player);
                    }
                    else if (BaseDoor is KeyLock)
                    {
                        var keyLock = (KeyLock)BaseDoor;
                        canUse = CanOpenKeyLock(keyLock, Player);
                    }
                }
                else
                {
                    canUse = true;
                }
                return canUse;
            }

            private bool CanOpenCodeLock(CodeLock door, BasePlayer player)
            {
                var canUse = false;
                var whitelist = door.whitelistPlayers;
                canUse = whitelist.Contains(player.userID);

                if (!canUse)
                {
                    canUse = player.CanBuild() && _checker.IsPlayerAuthorized();
                    if (canUse && _handler.ClansAvailable())
                    {
                        canUse = _handler.IsInClan(player);
                    }
                }

                PlaySound(canUse, door, player);
                return canUse;
            }

            private bool CanOpenKeyLock(KeyLock door, BasePlayer player)
            {
                var canUse = door.HasLockPermission(player) || player.CanBuild() && _checker.IsPlayerAuthorized();

                return canUse;
            }

            private void PlaySound(bool canUse, CodeLock door, BasePlayer player)
            {
                Effect.server.Run(canUse ? door.effectUnlocked.resourcePath : door.effectDenied.resourcePath,
                    player.transform.position, Vector3.zero);
            }
        }

        private class ToolCupboardChecker
        {
            public BasePlayer Player { get; protected set; }

            public ToolCupboardChecker(BasePlayer player)
            {
                Player = player;
            }

            public bool IsPlayerAuthorized()
            {
                return Player.IsBuildingAuthed();
            }
        }

        private class MasterKeyHolders
        {
            private Dictionary<string, PlayerSettings> _keyMasters;

            public MasterKeyHolders()
            {
                _keyMasters = new Dictionary<string, PlayerSettings>();
            }

            public void AddMaster(string id)
            {
                _keyMasters.Add(id, new PlayerSettings(false));
            }

            public void RemoveMaster(string id)
            {
                _keyMasters.Remove(id);
            }

            public void GiveMasterKey(string id)
            {
                PlayerSettings settings;
                var exists = _keyMasters.TryGetValue(id, out settings);
                if (exists)
                {
                    settings.IsMasterKeyHolder = true;
                }
            }

            public void RemoveMasterKey(string id)
            {
                PlayerSettings settings;
                var exists = _keyMasters.TryGetValue(id, out settings);
                if (exists)
                {
                    settings.IsMasterKeyHolder = false;
                }
            }

            public bool IsAKeyMaster(string id)
            {
                var isKeyMaster = false;
                PlayerSettings settings;
                var exists = _keyMasters.TryGetValue(id, out settings);
                if (exists)
                {
                    isKeyMaster = settings.IsMasterKeyHolder;
                }
                return isKeyMaster;
            }

            public void ToggleMasterMode(string id)
            {
                PlayerSettings settings;
                var exists = _keyMasters.TryGetValue(id, out settings);
                if (exists)
                {
                    settings.ToggleMasterMode();
                }
            }

            public bool HasMaster(string id)
            {
                return _keyMasters.ContainsKey(id);
            }
        }
        
        #endregion
    }
}