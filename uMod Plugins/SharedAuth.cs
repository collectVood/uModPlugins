using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Shared Auth", "Iv Misticos", "1.0.0")]
    [Description("Make sharing auth better")]
    public class SharedAuth : CovalencePlugin
    {
        #region Variables
        
        [PluginReference]
        // ReSharper disable once InconsistentNaming
        private Plugin Clans;

        private static SharedAuth _instance;
        private const string RustIo = "clans";
        private const string ClansName = "Clans";
        private const string RustClansHook = "SharedDoors now hooking to Rust:IO Clans";
        private const string RustClansNotFound = "Rust Clans has not been found.";
        private const string MasterPerm = "shareddoors.master";
        private MasterKeyHolders _holders;
        
        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            _instance = this;
            permission.RegisterPermission(MasterPerm, this);
            if (Clans == null)
            {
                Puts(RustClansNotFound);
            }
            else
            {
                Puts(RustClansHook);
            }
        }

        private void Unload()
        {
            _instance = null;
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == ClansName)
            {
                Puts(RustClansHook);
                Clans = plugin;
            }
        }

        private void OnPluginUnloaded(Plugin name)
        {
            if (name.Name == ClansName)
            {
                Puts(RustClansHook);
                Clans = null;
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            var iPlayer = covalence.Players.FindPlayerById(player.userID.ToString());
            if (player.IsAdmin || iPlayer.HasPermission(MasterPerm))
            {
                _holders.AddMaster(player.userID.ToString());
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var iPlayer = covalence.Players.FindPlayerById(player.userID.ToString());
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

        private class RustIoHandler
        {
            private const string GetClanOfPlayer = "GetClanOf";
            private const string GetClan = "GetClan";
            private const string Members = "members";
            public Plugin Clans { get; protected set; }
            public ulong OriginalPlayerID { get; protected set; }
            public DoorAuthorizer Door { get; protected set; }

            public RustIoHandler(DoorAuthorizer door)
            {
                if (door.BaseDoor is CodeLock)
                {
                    var codeLock = door.BaseDoor as CodeLock;
                    var whitelist = codeLock.whitelistPlayers;
                    if (whitelist.Count > 0)
                    {
                        OriginalPlayerID = whitelist[0];
                    }
                    else
                    {
                        OriginalPlayerID = 0;
                    }
                }
                Door = door;
                Clans = _instance.Clans;
            }

            public bool IsInClan(BasePlayer player)
            {
                var isInClan = false;
                if (ClansAvailable())
                {
                    var obj = Clans.CallHook(GetClanOfPlayer, OriginalPlayerID);
                    if (obj != null)
                    {
                        var clanName = obj.ToString();
                        var clan = Clans.CallHook(GetClan, clanName);
                        if (clan != null)
                        {
                            var jObject = JObject.FromObject(clan);
                            var members = (JArray)jObject.GetValue(Members);
                            var memberIds = members.ToObject<string[]>();
                            isInClan = memberIds.Contains(player.userID.ToString());
                        }
                    }
                }

                return isInClan;
            }

            public bool ClansAvailable()
            {
                return Clans != null;
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

        private class PlayerSettings
        {
            public bool IsMasterKeyHolder { get; set; }

            public PlayerSettings(bool isMasterKeyHolder)
            {
                IsMasterKeyHolder = isMasterKeyHolder;
            }

            public void ToggleMasterMode()
            {
                IsMasterKeyHolder = !IsMasterKeyHolder;
            }
        }
        
        #endregion
    }
}