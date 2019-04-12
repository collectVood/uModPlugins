using ConVar;
using Facepunch.Math;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Green", "Iv Misticos", "1.3.3")]
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
            
            Server.Broadcast(message, name, player.userID);
            
            player.NextChatTime = UnityEngine.Time.realtimeSinceStartup + 1.5f;
            
            var chatEntry = new Chat.ChatEntry
            {
                Message = message,
                UserId = player.userID,
                Username = name,
                Color = color,
                Time = Epoch.Current
            };
            
            Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, chatEntry);
            return true;
        }
    }
}