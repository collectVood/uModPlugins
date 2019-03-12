using ConVar;
using Facepunch.Math;

namespace Oxide.Plugins
{
    [Info("No Green", "JakeKillsAll & Iv Misticos", "1.3.0")]
    [Description("No Green Admin")]
    class NoGreen : RustPlugin
    {
        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = (BasePlayer)arg.Connection.player;
            var message = Chat.EscapeRichText(arg.GetString(0));
            var color = "#5af";
            
            rust.BroadcastChat($"<color={color}>{player.displayName}</color>", message, player.UserIDString);
            
            player.NextChatTime = UnityEngine.Time.realtimeSinceStartup + 1.5f;
            
            var chatEntry = new Chat.ChatEntry
            {
                Message = message,
                UserId = player.userID,
                Username = player.displayName,
                Color = color,
                Time = Epoch.Current
            };
            
            Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, chatEntry);
            return true;
        }
    }
}