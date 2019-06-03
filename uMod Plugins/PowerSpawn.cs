using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Power Spawn", "Iv Misticos", "1.0.3")]
    [Description("Control players' spawning")]
    class PowerSpawn : RustPlugin
    {
        #region Variables

        private int _worldSize;

        private readonly int _layerTerrain = LayerMask.NameToLayer("Terrain");

        private readonly Random _random = new Random();

        private static PowerSpawn _ins;
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Minimal Distance To Building")]
            public int DistanceBuilding = 10;

            [JsonProperty(PropertyName = "Minimal Distance To Collider")]
            public int DistanceCollider = 10;

            [JsonProperty(PropertyName = "Maximum Number Of Attempts To Find A Location")]
            public int AttemptsMax = 200;

            [JsonProperty(PropertyName = "Location Management Command")]
            public string LocationCommand = "loc";

            [JsonProperty(PropertyName = "Location Management Permission")]
            public string LocationPermission = "powerspawn.location";

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
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

        #region Work with Data

        private PluginData _data;

        private class PluginData
        {
            
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }
        
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permission", "nope" },
                { "Location: Syntax", "Location Syntax:\n" +
                                     "new (Name) - Create a new location with a specified name\n" +
                                     "delete (ID) - Delete a location with the specified ID\n" +
                                     "edit (ID) <Parameter 1> <Value> <...> - Edit a location with the specified ID" },
                { "Location: Edit Syntax", "Location Edit Parameters:\n" +
                                          "move (x;y;z / here) - Move a location to the specified position\n" +
                                          "group (ID / reset) - Set group of a location or reset the group" },
                { "Location: Unable To Parse Position", "Unable to parse the position" }
            }, this);
        }

        private void OnServerInitialized()
        {
            _ins = this;
            _worldSize = ConVar.Server.worldsize;
            LoadData();

            AddCovalenceCommand(_config.LocationCommand, nameof(CommandLocation));
        }

        private object OnPlayerRespawn(BasePlayer player)
        {
            var position = FindPosition();
            if (!position.HasValue)
            {
                PrintDebug($"Haven't found a position for {player.displayName}");
                return null;
            }
            
            PrintDebug($"Found position for {player.displayName}: {position}");
            
            return new BasePlayer.SpawnPoint
            {
                pos = position.Value
            };
        }

        #endregion
        
        #region Commands

        private void CommandLocation(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(_config.LocationPermission))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }
        }

        private class CommandLocationData
        {
            public IPlayer Player;
            // TODO: Location
            
            private const int FirstArgumentIndex = 2;
            
            public void Apply(string[] args)
            {
                for (var i = FirstArgumentIndex; i + 1 < args.Length; i += 2)
                {
                    switch (args[i])
                    {
                        case "move":
                        {
                            var position = ParseVector(args[i + 1]);
                            if (!position.HasValue)
                            {
                                Player.Reply(GetMsg("Location: Unable To Parse Position", Player.Id));
                                break;
                            }
                            
                            // TODO: Set location
                            break;
                        }

                        case "group":
                        {
                            
                        }
                    }
                }
            }

            public Vector3? ParseVector(string argument)
            {
                var vector = new Vector3();
                
                if (argument == "here")
                {
                    Player.Position(out vector.x, out vector.y, out vector.z);
                }
                else
                {
                    var coordinates = argument.Split(';');
                    if (coordinates.Length != 3 || !float.TryParse(coordinates[0], out vector.x) ||
                        !float.TryParse(coordinates[1], out vector.y) || !float.TryParse(coordinates[2], out vector.z))
                    {
                        return null;
                    }
                }
                
                return vector;
            }
        }
        
        #endregion
        
        #region Helpers

        private Vector3? FindPosition()
        {
            for (var i = 0; i < _config.AttemptsMax; i++)
            {
                var position = TryFindPosition();
                if (position.HasValue)
                    return position;
            }

            return null;
        }

        private Vector3? TryFindPosition()
        {
            var position = new Vector3(GetRandomPosition(), 0, GetRandomPosition());
            var height = TerrainMeta.HeightMap.GetHeight(position);
            if (height > 0)
                position.y = height;
            else
                return null;

            return CheckBadBuilding(position) || CheckBadCollider(position) ? (Vector3?) null : position;
        }

        private int GetRandomPosition() => _random.Next(_worldSize / -2, _worldSize / 2);

        private void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug($"{Name} > " + message);
        }

        private bool CheckBadBuilding(Vector3 position)
        {
            var buildings = new List<BuildingBlock>();
            Vis.Entities(position, _config.DistanceBuilding, buildings, Layers.Construction);
            return buildings.Count > 0;
        }

        private bool CheckBadCollider(Vector3 position)
        {
            var colliders = new List<Collider>();
            Vis.Components(position, _config.DistanceCollider, colliders);
            foreach (var collider in colliders)
            {
                var gameObject = collider.gameObject;
                if (gameObject.layer == _layerTerrain)
                    continue;
                
                return true;
            }
            
            return false;
        }

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        #endregion
    }
}