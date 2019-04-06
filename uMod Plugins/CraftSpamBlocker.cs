using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Craft Spam Blocker", "Iv Misticos & Wulf/lukespragg", "1.0.0")]
    [Description("Prevents items from being crafted if the player's inventory is full")]
    class CraftSpamBlocker : RustPlugin
    {
        #region Variables
        
        
        
        #endregion
        
        #region Configuration

        private Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Incorrect Crafts To Block")]
            public int IncorrectNeeded = 5;

            [JsonProperty(PropertyName = "Incorrect Craft Lifetime (In Seconds)")]
            public float IncorrectCraftLifetime = 5f;

            [JsonProperty(PropertyName = "Block Time")]
            public float BlockTime = 300f;

            [JsonProperty(PropertyName = "Rage Mode")]
            public bool Rage = true;
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
                Config.WriteObject(_config, false, $"{Interface.GetMod().ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Inventory Full", "Item was not crafted, inventory is full!" },
                { "Limited Crafting", "Your crafting is limited. Please, stop spamming." }
            }, this);
        }

        private object OnItemCraft(ItemCraftTask task) => Process(task, false);

        private object OnItemCraftFinished(ItemCraftTask task) => Process(task, true);

        #endregion
        
        #region Controller

        private class PlayerController : FacepunchBehaviour
        {
            public BasePlayer player;

            public uint lastCraftTime;
            public uint blockStartTime;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
        }
        
        #endregion
        
        #region Helpers

        private object Process(ItemCraftTask task, bool isFinished)
        {
            var player = task.owner; // Getting the player
            if (player == null)
                return null;
            
            var inventory = player.inventory;
            if (inventory.containerMain.itemList.Count < inventory.containerMain.capacity ||
                inventory.containerBelt.itemList.Count < inventory.containerBelt.capacity)
                return null; // Return if inventory is NOT full

            return null;
        }

//        private void Cancel(ItemCraftTask task, bool cancelAll)
//        {
//            
//            var crafter = inventory.crafting;
//            NextTick(() =>
//            {
//                if (cancelAll) crafter.CancelAll(false);
//                else crafter.CancelTask(task.taskUID, true);
//            });
//
//            player.ChatMessage(GetMsg("Inventory Full", player.UserIDString));
//        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}