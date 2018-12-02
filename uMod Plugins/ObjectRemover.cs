using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Libraries;
using UnityEngine;
using Time = UnityEngine.Time;
// ReSharper disable UnusedMember.Local

namespace Oxide.Plugins
{
    [Info("ObjectRemover", "Iv Misticos", "3.0.1")]
    [Description("Removes furnaces, lanterns, campfires, buildings etc. on command")]
    class ObjectRemover : RustPlugin
    {
        #region Variables
        
        private const string ShortnameCupboard = "cupboard.tool";

        #endregion

        #region Configuration

        private Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Object Command Permission")]
            public string PermissionUse = "objectremover.use";
            
            [JsonProperty(PropertyName = "Object Command")]
            public string Command = "object";
            
            public string Prefix = "[<color=#ffbf00> Object Remover </color>] ";
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
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Rights", "You do not have enough rights" },
                { "Count", "We found {count} entities in {time}s." },
                { "Removed", "You have removed {count} entities in {time}s." },
                { "Help", "Object command usage:\n" +
                          "/object (entity) (action) [radius]\n" +
                          "entity: part of shortname or 'all'\n" +
                          "action: count or remove\n" +
                          "radius: optional, radius" },
                { "No Console", "Please log in as a player to use that command" }
            }, this);
        }

        private void OnServerInitialized()
        {
            LoadDefaultMessages();
            LoadConfig();
            
            if (!permission.PermissionExists(_config.PermissionUse))
                permission.RegisterPermission(_config.PermissionUse, this);

            var cmdLib = GetLibrary<Command>();
            cmdLib.AddChatCommand(_config.Command, this, CommandChatObject);
            cmdLib.AddConsoleCommand(_config.Command, this, CommandConsoleObject);
        }
        
        #endregion

        #region Commands

        private void CommandChatObject(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;
            if (!permission.UserHasPermission(id, _config.PermissionUse))
            {
                player.ChatMessage(_config.Prefix + GetMsg("No Rights", id));
                return;
            }

            if (args.Length < 2)
            {
                player.ChatMessage(_config.Prefix + GetMsg("Help", id));
                return;
            }

            var entity = args[0];
            var isCount = args[1].Equals("count");
            float radius;
            if (args.Length == 2 || !float.TryParse(args[2], out radius))
                radius = 10f;

            var before = Time.realtimeSinceStartup;
            var objects = FindObjects(player.transform.position, radius, entity);
            var count = objects.Count;

            if (isCount)
            {
                player.ChatMessage(_config.Prefix + GetMsg("Count", id).Replace("{count}", count.ToString()).Replace("{time}", (Time.realtimeSinceStartup - before).ToString("0.###")));
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    var ent = objects[i];
                    if (ent == null || ent.IsDestroyed)
                        continue;
                    ent.Kill();
                }
                
                player.ChatMessage(_config.Prefix + GetMsg("Removed").Replace("{count}", count.ToString()).Replace("{time}", (Time.realtimeSinceStartup - before).ToString("0.###")));
            }
        }
        
        private bool CommandConsoleObject(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                arg.ReplyWith(GetMsg("No Console"));
                return true;
            }
            
            CommandChatObject(player, string.Empty, arg.Args ?? new string[0]);
            return false;
        }

        #endregion

        #region Plugin Helpers

        private List<BaseEntity> FindObjects(Vector3 startPos, float radius, string entity)
        {
            var entities = new List<BaseEntity>();
            var isAll = entity.Equals("all");
            if (radius > 0)
            {
                Vis.Entities(startPos, radius, entities);
                if (isAll)
                    return entities;

                var entitiesCount = entities.Count;
                for (var i = entitiesCount - 1; i >= 0; i--)
                {
                    var ent = entities[i];
                    if (ent.ShortPrefabName.IndexOf(entity, StringComparison.CurrentCultureIgnoreCase) == -1)
                        entities.RemoveAt(i);
                }
            }
            else
            {
                var ents = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
                var entsCount = ents.Length;
                for (var i = 0; i < entsCount; i++)
                {
                    var ent = ents[i];
                    if (isAll || ent.ShortPrefabName.IndexOf(entity, StringComparison.CurrentCultureIgnoreCase) != -1)
                        entities.Add(ent);
                }
            }

            return entities;
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}