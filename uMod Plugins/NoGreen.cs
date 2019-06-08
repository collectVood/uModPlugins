using ConVar;
using Facepunch;
using Facepunch.Math;
using UnityEngine;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("No Green", "Iv Misticos", "1.3.4")]
    [Description("Remove admins' green names")]
    class NoGreen : RustPlugin
    {
        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = (BasePlayer)arg.Connection.player;
            if (player == null || !player.IsAdmin)
                return null;
            
            var message = arg.GetString(0).EscapeRichText(); // That's what devs use
            var name = player.displayName.EscapeRichText();
            var color = "#5af";
            
            if (Chat.serverlog)
            {
                DebugEx.Log($"[CHAT] {player} : {message}", StackTraceLogType.None);
            }
            
            player.NextChatTime = Time.realtimeSinceStartup + 1.5f;
            
            var chatEntry = new Chat.ChatEntry
            {
                Message = message,
                UserId = player.userID,
                Username = name,
                Color = color,
                Time = Epoch.Current
            };
            
            RCon.Broadcast(RCon.LogType.Chat, chatEntry);
            
            if (ConVar.Server.globalchat)
            {
                ConsoleNetwork.BroadcastToAllClients("chat.add2", player.userID, message, name, color, 1f);
            }
            else
            {
                var num2 = 2500f;
                foreach (var target in BasePlayer.activePlayerList)
                {
                    var sqrMagnitude = (target.transform.position - player.transform.position).sqrMagnitude;
                    if (sqrMagnitude <= num2)
                    {
                        ConsoleNetwork.SendClientCommand(target.net.connection, "chat.add2", player.userID,
                            message, name, color, Mathf.Clamp01(num2 - sqrMagnitude + 0.2f));
                    }
                }
            }

            return true;
        }
    }
}