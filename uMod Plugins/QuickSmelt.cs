using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Quick Smelt", "Iv Misticos", "5.0.0")]
    [Description("Increases the speed of the furnace smelting")]
    class QuickSmelt : RustPlugin
    {
        #region Variables
        
        private static QuickSmelt _instance;

//        private static HashSet<string> _rawMeatNames = new HashSet<string>
//        {
//            "bearmeat",
//            "meat.boar",
//            "wolfmeat.raw",
//            "humanmeat.raw",
//            "fish.raw",
//            "chicken.raw",
//            "deermeat.raw",
//            "horsemeat.raw"
//        };
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "quicksmelt.use";
            
            [JsonProperty(PropertyName = "Speed Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SpeedMultipliers = new Dictionary<string, float>
            {
                { "furnace.shortname", 1.0f }
            };
            
            [JsonProperty(PropertyName = "Fuel Usage Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> FuelMultipliers = new Dictionary<string, float>
            {
                { "furnace.shortname", 1.0f }
            };
            
            [JsonProperty(PropertyName = "Output Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, Dictionary<string, float>> OutputMultipliers = new Dictionary<string, Dictionary<string, float>>
            {
                { "furnace.shortname", new Dictionary<string, float>
                {
                    { "item.shortname", 1.0f }
                } }
            };

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
                Config.WriteObject(_config, false, $"{Interface.GetMod().ConfigDirectory}/{Name}.jsonError");
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

        private void Unload()
        {
            var ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>();
            PrintDebug($"Processing BaseOven(s).. Amount: {ovens.Length}.");
            
            for (var i = 0; i < ovens.Length; i++)
            {
                var oven = ovens[i];
                var component = oven.GetComponent<FurnaceController>();

                if (oven.IsOn())
                {
                    PrintDebug("Oven is on. Restarted cooking");
                    component.StopCooking();
                    oven.StartCooking();
                }

                UnityEngine.Object.Destroy(component);
            }
            
            PrintDebug("Done.");
        }

        private void OnServerInitialized()
        {
            _instance = this;
            permission.RegisterPermission(_config.Permission, this);

            var ovens = UnityEngine.Object.FindObjectsOfType<BaseOven>();
            PrintDebug($"Processing BaseOven(s).. Amount: {ovens.Length}.");
            
            for (var i = 0; i < ovens.Length; i++)
            {
                var oven = ovens[i];
                OnEntitySpawned(oven);
                var component = oven.gameObject.GetComponent<FurnaceController>();
                
                if (!oven.IsOn())
                    continue;

                // Invokes are actually removed at the end of a frame, meaning you can get multiple invokes at once after reloading plugin
                NextFrame(() =>
                {
                    if (oven == null || oven.IsDestroyed)
                        return;

                    component.StopCooking();
                    component.StartCooking();
                });
            }
            
            PrintDebug("Done.");
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var oven = entity as BaseOven;
            if (oven == null)
                return;

            oven.gameObject.AddComponent<FurnaceController>();
        }

        private object OnOvenToggle(StorageContainer oven, BasePlayer player)
        {
            if (oven is BaseFuelLightSource || oven.needsBuildingPrivilegeToUse && !player.CanBuild())
                return null;

            var component = oven.gameObject.GetComponent<FurnaceController>();
            if (oven.IsOn())
                component.StopCooking();
            else
                component.StartCooking();
            
            return false;
        }
        
        #endregion
        
        #region Helpers

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Debug.Log($"DEBUG ({_instance.Name}) > " + message);
        }
        
        #endregion
        
        #region Controller
		
        public class FurnaceController : FacepunchBehaviour
        {
            private BaseOven _oven;

            private BaseOven Furnace
            {
                get
                {
                    if (_oven == null)
                        _oven = GetComponent<BaseOven>();

                    return _oven;
                }
            }

            private float SpeedMultiplier
            {
                get
                {
                    float modifier;
                    if (!_config.SpeedMultipliers.TryGetValue(Furnace.ShortPrefabName, out modifier))
                        modifier = 1.0f;

                    return 0.5f * modifier;
                }
            }

            private float FuelUsageMultiplier
            {
                get
                {
                    float modifier;
                    if (!_config.FuelMultipliers.TryGetValue(Furnace.ShortPrefabName, out modifier))
                        modifier = 1.0f;

                    return modifier;
                }
            }

            private float OutputMultiplier(string shortname)
            {
                Dictionary<string, float> modifiers;
                float modifier;
                if (!_config.OutputMultipliers.TryGetValue(Furnace.ShortPrefabName, out modifiers) || !modifiers.TryGetValue(shortname, out modifier))
                    return 1.0f;

                return modifier;
            }

            private Item FindBurnable()
            {
                if (Furnace.inventory == null)
                    return null;
                
                foreach (var item in Furnace.inventory.itemList)
                {
                    var component = item.info.GetComponent<ItemModBurnable>();
                    if (component && (Furnace.fuelType == null || item.info == Furnace.fuelType))
                    {
                        return item;
                    }
                }
                
                return null;
            }

            public void Cook()
            {
                var item = FindBurnable();
                if (item == null)
                {
                    StopCooking();
                    return;
                }

                Furnace.inventory.OnCycle(SpeedMultiplier);
                var slot = Furnace.GetSlot(BaseEntity.Slot.FireMod);
                if (slot)
                {
                    slot.SendMessage("Cook", SpeedMultiplier, SendMessageOptions.DontRequireReceiver);
                }
                
                var component = item.info.GetComponent<ItemModBurnable>();
                item.fuel -= SpeedMultiplier * (Furnace.cookingTemperature / 200f) * FuelUsageMultiplier;
                if (!item.HasFlag(global::Item.Flag.OnFire))
                {
                    item.SetFlag(global::Item.Flag.OnFire, true);
                    item.MarkDirty();
                }
                
                if (item.fuel <= 0f)
                {
                    ConsumeFuel(item, component);
                }
                
                PrintDebug("Cook");
            }

            private void ConsumeFuel(Item fuel, ItemModBurnable burnable)
            {
                if (Furnace.allowByproductCreation && burnable.byproductItem != null && Random.Range(0f, 1f) > burnable.byproductChance)
                {
                    var def = burnable.byproductItem;
                    PrintDebug(def.shortname);
                    var item = ItemManager.Create(def, (int) (burnable.byproductAmount * OutputMultiplier(def.shortname))); // TODO: Work on fuel
                    if (!item.MoveToContainer(Furnace.inventory))
                    {
                        StopCooking();
                        item.Drop(Furnace.inventory.dropPosition, Furnace.inventory.dropVelocity);
                    }
                }
                
                if (fuel.amount <= 1)
                {
                    fuel.Remove();
                    return;
                }
                
                fuel.amount--;
                fuel.fuel = burnable.fuelAmount;
                fuel.MarkDirty();
            }
            
            public void StartCooking()
            {
                if (FindBurnable() == null)
                {
                    PrintDebug("No burnable.");
                    return;
                }

                PrintDebug("Starting cooking..");
                Furnace.inventory.temperature = Furnace.cookingTemperature;
                Furnace.UpdateAttachmentTemperature();
                
                Furnace.CancelInvoke(Cook);
                Furnace.InvokeRepeating(Cook, SpeedMultiplier, SpeedMultiplier);
                Furnace.SetFlag(BaseEntity.Flags.On, true);
            }

            public void StopCooking()
            {
                PrintDebug("Stopping cooking..");
                Furnace.CancelInvoke(Cook);
                Furnace.StopCooking();
            }
        }
        
        #endregion
    }
}