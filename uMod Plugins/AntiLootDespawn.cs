using UnityEngine;
using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AntiLootDespawn", "Iv Misticos", "2.0.0")]
    [Description("Change loot despawn time in cupboard radius")]
    public class AntiLootDespawn : RustPlugin
    {
        private float _despawnMultiplier = 2.0f;
        private bool _enabled = true;

        private void Init()
        {
            permission.RegisterPermission("antilootdespawn.check", this);
            permission.RegisterPermission("antilootdespawn.multiplier", this);
            permission.RegisterPermission("antilootdespawn.enabled", this);
            _despawnMultiplier = GetConfigEntry("multiplier", 2.0f);
            _enabled = GetConfigEntry("enabled", true);
        }

        private void Unloaded()
        {
            foreach(var item in Resources.FindObjectsOfTypeAll<DroppedItem>().Where(c => c.isActiveAndEnabled))
            {
                item.CancelInvoke(nameof(DroppedItem.IdleDestroy));
                item.Invoke(nameof(DroppedItem.IdleDestroy), item.GetDespawnDuration());
            }
        }

        private void OnEntitySpawned(BaseEntity entity) => SetDespawnTime(entity as DroppedItem);

        private void SetDespawnTime(DroppedItem item)
        {
            if (!_enabled)
                return;
            if (item == null)
                return;

            var entityRadius = Physics.OverlapSphere(item.transform.position, 0.5f, LayerMask.GetMask("Trigger"));

            foreach (var cupboard in entityRadius)
            {
                if (cupboard.GetComponentInParent<BuildingPrivlidge>() != null)
                {
                    item.CancelInvoke(nameof(DroppedItem.IdleDestroy));
                    item.Invoke(nameof(DroppedItem.IdleDestroy), _despawnMultiplier * item.GetDespawnDuration());
                }
            }
        }

        [ConsoleCommand("antilootdespawn.multiplier")]
        private void CmdMultiplier(ConsoleSystem.Arg args)
        {
            if(args.Player() != null)
            {
                if (!args.Player().IsAdmin && !permission.UserHasPermission(args.Player().UserIDString, "antilootdespawn.multiplier"))
                    return;
            }

            if (args.HasArgs())
            {
                _despawnMultiplier = Convert.ToSingle(args.Args[0]);
                Config["multiplier"] = _despawnMultiplier;
                SaveConfig();
            }
            args.ReplyWith($"antilootdespawn.multiplier = {_despawnMultiplier}");
        }

        [ConsoleCommand("antilootdespawn.enabled")]
        private void CmdEnabled(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
            {
                if (!args.Player().IsAdmin && !permission.UserHasPermission(args.Player().UserIDString, "antilootdespawn.enabled"))
                    return;
            }

            if (args.HasArgs())
            {
                _enabled = (args.Args[0] == "true" ? true : args.Args[0] == "false" ? false : args.Args[0] == "1" ? true : args.Args[0] == "0" ? false : true);
                Config["enabled"] = _enabled;
                SaveConfig();
            }
            args.ReplyWith($"antilootdespawn.enabled = {_enabled}");
        }

        [ConsoleCommand("antilootdespawn")]
        private void CmdList(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
            {
                if (!args.Player().IsAdmin && !permission.UserHasPermission(args.Player().UserIDString, "antilootdespawn"))
                    return;
            }

            args.ReplyWith($"antilootdespawn.enabled = {_enabled}\nantilootdespawn.multiplier = {_despawnMultiplier}");
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file for AntiLootDespawn");
            Config.Clear();
            Config["multiplier"] = 2.0f;
            Config["enabled"] = true;
            SaveConfig();
        }

        private T GetConfigEntry<T>(string configEntry, T defaultValue)
        {
            if (Config[configEntry] == null)
            {
                Config[configEntry] = defaultValue;
                SaveConfig();
            }
            return (T)Convert.ChangeType(Config[configEntry], typeof(T));
        }
    }
}