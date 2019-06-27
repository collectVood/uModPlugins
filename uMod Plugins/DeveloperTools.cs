using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Developer Tools", "Iv Misticos", "1.0.0")]
    [Description("Provides some development tools for developers like finding triggers")]
    class DeveloperTools : RustPlugin
    {
        #region Variables
        
        List<Item> createdItems = new List<Item>();
        
        #endregion
        
        #region Hooks
        
        private void Init()
        {
            AddCovalenceCommand("dt.colliders", nameof(CommandGetColliders));
            AddCovalenceCommand("dt.components", nameof(CommandGetComponents));
            AddCovalenceCommand("dt.entities", nameof(CommandGetEntities));
            AddCovalenceCommand("dt.monument", nameof(CommandGetMonument));
            AddCovalenceCommand("dt.capacity", nameof(CommandCapacity));
            AddCovalenceCommand("dt.hostile", nameof(CommandNoHostile));
            AddCovalenceCommand("dtext.load", nameof(CommandExtensionLoad));
            AddCovalenceCommand("dtext.reload", nameof(CommandExtensionReload));
            AddCovalenceCommand("dtext.unload", nameof(CommandExtensionUnload));
            AddCovalenceCommand("dtext.list", nameof(CommandExtensionList));
            AddCovalenceCommand("dt.cei", nameof(CommandCreateEmptyItems));
            AddCovalenceCommand("dt.cleari", nameof(CommandClearItems));
            AddCovalenceCommand("dt.rungc", nameof(CommandRunGC));
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

        private void CommandCapacity(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
                return;

            var newCapacity = 0;
            if (args.Length != 0 && !int.TryParse(args[0], out newCapacity))
                return;

            RaycastHit hit;
            if (!Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, 50f))
                return;

            var entity = hit.GetEntity();
            if (entity == null || !(entity is StorageContainer))
                return;

            var container = entity as StorageContainer;
            if (container == null)
                return;

            if (args.Length == 0)
            {
                player.Reply($"{container.inventory.capacity}");
                return;
            }
            
            container.inventory.capacity = newCapacity;
        }

        private void CommandNoHostile(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
                return;

            basePlayer.unHostileTime = 0;
            basePlayer.ClientRPCPlayer( null, basePlayer, "SetHostileLength", 0);
        }

        private void CommandExtensionLoad(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
                return;

            Interface.Oxide.LoadExtension(args[0]);
        }

        private void CommandExtensionReload(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
                return;

            Interface.Oxide.ReloadExtension(args[0]);
        }

        private void CommandExtensionUnload(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
                return;

            Interface.Oxide.UnloadExtension(args[0]);
        }

        private void CommandExtensionList(IPlayer player, string command, string[] args)
        {
            var extensions = Interface.Oxide.GetAllExtensions();
            var table = new TextTable();
            table.AddColumns("Name", "Author", "Filename", "Version");
            
            foreach (var ext in extensions)
            {
                table.AddRow(ext.Name, ext.Author, ext.Filename.Substring(ext.Filename.LastIndexOf('\\') + 1), ext.Version.ToString());
            }
            
            player.Reply(table.ToString());
        }

        private void CommandCreateEmptyItems(IPlayer player, string command, string[] args)
        {
            if (args.Length < 2)
                return;

            var itemDef = ItemManager.FindItemDefinition(args[0]);
            if (itemDef == null)
                return;

            int amount;
            if (!int.TryParse(args[1], out amount) || amount <= 0)
                return;
            
            player.Reply("Started creating");
            for (var i = 0; i < amount; i++)
            {
                createdItems.Add(ItemManager.Create(itemDef));
            }

            player.Reply($"Created {amount} items with shortname {args[0]}");
            player.Reply($"Total created items: {createdItems.Count}");
        }

        private void CommandClearItems(IPlayer player, string command, string[] args)
        {
            createdItems.Clear();
            createdItems = new List<Item>();
        }

        private void CommandRunGC(IPlayer player, string command, string[] args)
        {
            GC.Collect(GC.MaxGeneration);
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