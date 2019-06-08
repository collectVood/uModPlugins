using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Developer Tools", "Iv Misticos", "1.0.0")]
    [Description("Provides some development tools for developers like finding triggers")]
    class DeveloperTools : RustPlugin
    {
        #region Hooks
        
        private void Init()
        {
            AddCovalenceCommand("dtcolliders", nameof(CommandGetColliders));
            AddCovalenceCommand("dtcomponents", nameof(CommandGetComponents));
            AddCovalenceCommand("dtentities", nameof(CommandGetEntities));
            AddCovalenceCommand("dtmonument", nameof(CommandGetMonument));
        }
        
        #endregion
        
        #region Commands

        private void CommandGetColliders(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
                return;
            
            float radius;
            if (args.Length < 1 || !float.TryParse(args[0], out radius))
                radius = 5f;

            var output = new List<Collider>();
            Vis.Colliders(basePlayer.transform.position, radius, output);

            var table = new TextTable();
            table.AddColumns("name", "class", "is trigger");
            
            foreach (var entry in output)
            {
                table.AddRow(entry.name, entry.GetType().FullName, entry.isTrigger.ToString());
            }
            
            basePlayer.ConsoleMessage(table.ToString());
        }

        private void CommandGetComponents(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
                return;
            
            float radius;
            if (args.Length < 1 || !float.TryParse(args[0], out radius))
                radius = 5f;

            var output = new List<Component>();
            Vis.Components(basePlayer.transform.position, radius, output);

            var table = new TextTable();
            table.AddColumns("name", "class");
            
            foreach (var entry in output)
            {
                table.AddRow(entry.name, entry.GetType().FullName);
            }
            
            basePlayer.ConsoleMessage(table.ToString());
        }

        private void CommandGetEntities(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
                return;
            
            float radius;
            if (args.Length < 1 || !float.TryParse(args[0], out radius))
                radius = 5f;

            var output = new List<BaseEntity>();
            Vis.Entities(basePlayer.transform.position, radius, output);

            var table = new TextTable();
            table.AddColumns("name", "class", "short prefab");
            
            foreach (var entry in output)
            {
                table.AddRow(entry.name, entry.GetType().FullName, entry.ShortPrefabName);
            }
            
            basePlayer.ConsoleMessage(table.ToString());
        }

        private void CommandGetMonument(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
                return;
            
            basePlayer.ConsoleMessage(GetMonumentName(basePlayer.transform.position));
        }
        
        #endregion
        
        #region Helpers

        private string GetMonumentName(Vector3 position)
        {
            var monuments = TerrainMeta.Path.Monuments;
            foreach (var monument in monuments)
            {
                var obb = new OBB(monument.transform.position, Quaternion.identity, monument.Bounds);
                if (obb.Contains(position))
                    return monument.name;
            }

            return string.Empty;
        }
        
        #endregion
    }
}