using System.Collections.Generic; 
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Radio", "Iv Misticos", "1.0.0")]
    [Description("Broadcast your voice")]
    class AdminRadio : RustPlugin
    {
        #region Variables

        private List<BasePlayer> ActiveSpeakers = new List<BasePlayer>();

        private const string PermSpeak = "adminradio.speak";
        private const string PermListen = "adminradio.listen";
        
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Only Players", "Only players can use this command!" },
                { "No Permission", "You don't have enough permissions to do that." },
                { "Enabled", "You have successfully become a speaker." },
                { "Disabled", "You are not a speaker now." }
            }, this);
        }

        private void Init()
        {
            permission.RegisterPermission(PermSpeak, this);
            permission.RegisterPermission(PermListen, this);
            
            cmd.AddConsoleCommand("adminradio.toggle", this, CommandConsoleToggle);
        }
        
        private void OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (!ActiveSpeakers.Contains(player))
                return;

            Broadcast(player, data);
        }
        
        #endregion
        
        #region Commands

        private bool CommandConsoleToggle(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                arg.ReplyWith(GetMsg("Only Players"));
                return true;
            }

            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, PermSpeak))
            {
                arg.ReplyWith(GetMsg("No Permission", player.UserIDString));
                return true;
            }

            if (ActiveSpeakers.Remove(player))
            {
                arg.ReplyWith(GetMsg("Disabled", player.UserIDString));
            }
            else
            {
                ActiveSpeakers.Add(player);
                arg.ReplyWith(GetMsg("Enabled", player.UserIDString));
            }

            return true;
        }
        
        #endregion
        
        #region Helpers

        private void Broadcast(BasePlayer player, byte[] data)
        {
            if (!Net.sv.write.Start())
                return;
            Net.sv.write.PacketID(Message.Type.VoiceData);
            Net.sv.write.UInt32(player.net.ID);
            Net.sv.write.BytesWithSize(data);
            
            var connections = GetListeningConnections();
            Net.sv.write.Send(new SendInfo(connections)
            {
                priority = Priority.Immediate
            });
        }

        private IEnumerable<Connection> GetListeningConnections()
        {
            var playersCount = BasePlayer.activePlayerList.Count;
            for (var i = 0; i < playersCount; i++)
            {
                var player = BasePlayer.activePlayerList[i];
                if (permission.UserHasPermission(player.UserIDString, PermListen))
                    yield return player.net.connection;
            }
        }

        private string GetMsg(string key, string id = null) => lang.GetMessage(key, this, id);

        #endregion
    }
}