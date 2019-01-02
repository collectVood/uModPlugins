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
            
            [JsonProperty(PropertyName = "Use Permission")]
            public bool UsePermission = true;
            
            [JsonProperty(PropertyName = "Speed Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SpeedMultipliers = new Dictionary<string, float>
            {
                { "furnace.shortname", 1.0f }
            };
            
            [JsonProperty(PropertyName = "Fuel Usage Speed Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> FuelSpeedMultipliers = new Dictionary<string, float>
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
            }

            timer.Once(1f, () =>
            {
                for (var i = 0; i < ovens.Length; i++)
                {
                    var oven = ovens[i];
                    var component = oven.gameObject.GetComponent<FurnaceController>();

                    if (oven == null || oven.IsDestroyed || !oven.IsOn() || !CanUse(oven.OwnerID))
                        continue;

                    component.StartCooking();
                }
            });
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
            if (oven.IsOn() && CanUse(oven.OwnerID))
                component.StopCooking();
            else
                component.StartCooking();
            
            return false;
        }
        
        #endregion
        
        #region Helpers

        private bool CanUse(ulong id) =>
            !_config.UsePermission || permission.UserHasPermission(id.ToString(), _config.Permission);

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

            private float FuelSpeedMultiplier
            {
                get
                {
                    float modifier;
                    if (!_config.FuelSpeedMultipliers.TryGetValue(Furnace.ShortPrefabName, out modifier))
                        modifier = 1.0f;

                    return modifier;
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

                SmeltItems(SpeedMultiplier);
                var slot = Furnace.GetSlot(BaseEntity.Slot.FireMod);
                if (slot)
                {
                    slot.SendMessage("Cook", 0.5f, SendMessageOptions.DontRequireReceiver);
                }
                
                var component = item.info.GetComponent<ItemModBurnable>();
                item.fuel -= 0.5f * (Furnace.cookingTemperature / 200f) * FuelSpeedMultiplier;
                if (!item.HasFlag(global::Item.Flag.OnFire))
                {
                    item.SetFlag(global::Item.Flag.OnFire, true);
                    item.MarkDirty();
                }
                
                if (item.fuel <= 0f)
                {
                    ConsumeFuel(item, component);
                }
            }

            private void ConsumeFuel(Item fuel, ItemModBurnable burnable)
            {
                if (Furnace.allowByproductCreation && burnable.byproductItem != null && Random.Range(0f, 1f) > burnable.byproductChance)
                {
                    var def = burnable.byproductItem;
                    var item = ItemManager.Create(def, (int) (burnable.byproductAmount * OutputMultiplier(def.shortname))); // It's fuel multiplier
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
                
                fuel.amount -= (int) FuelUsageMultiplier;
                fuel.fuel = burnable.fuelAmount;
                fuel.MarkDirty();
            }

            private void SmeltItems(float delta)
            {
                for (var i = 0; i < Furnace.inventory.itemList.Count; i++)
                {
                    // Getting item and checking if it's valid
                    var item = Furnace.inventory.itemList[i];
                    if (item == null || !item.IsValid())
                        continue;

                    // Getting cookable
                    var cookable = item.info.GetComponent<ItemModCookable>();
                    if (cookable == null)
                        continue;

                    // It's cook time!
                    var cooktimeLeft = cookable.cookTime;
                    
                    // What about temperature?
                    var temperature = item.temperature;
                    if (temperature < cookable.lowTemp || temperature > cookable.highTemp || cooktimeLeft < 0f)
                    {
                        if (!cookable.setCookingFlag || !item.HasFlag(global::Item.Flag.Cooking)) continue;
                        item.SetFlag(global::Item.Flag.Cooking, false);
                        item.MarkDirty();
                        continue;
                    }
                    
                    // Setting cooking flag
                    if (cookable.setCookingFlag && !item.HasFlag(global::Item.Flag.Cooking))
                    {
                        item.SetFlag(global::Item.Flag.Cooking, true);
                        item.MarkDirty();
                    }
                    
                    // Time ran out?
                    cooktimeLeft -= delta;
                    if (cooktimeLeft > 0f)
                    {
                        continue;
                    }
                    
                    // Changing amount
                    var position = item.position;
                    if (item.amount > 1)
                    {
                        // I don't know why cooktimeLeft is used here. It was in game code
                        cooktimeLeft = cookable.cookTime;
                        item.amount--;
                        item.MarkDirty();
                    }
                    else
                    {
                        item.Remove();
                    }

                    // What if nothing is produced?
                    if (cookable.becomeOnCooked == null) continue;

                    // Let's create an item!
                    var item2 = ItemManager.Create(cookable.becomeOnCooked,
                        (int) (cookable.amountOfBecome * OutputMultiplier(cookable.becomeOnCooked.shortname))); // It's an another one output multiplier, but not for fuel

                    // Some checks
                    if (item2 == null || item2.MoveToContainer(item.parent, position) ||
                        item2.MoveToContainer(item.parent))
                        continue;
                    
                    // Dropping item and stopping cooking if oven is full
                    item2.Drop(item.parent.dropPosition, item.parent.dropVelocity);
                    if (!item.parent.entityOwner) continue;
                    StopCooking();
                }
            }
            
            public void StartCooking()
            {
                if (FindBurnable() == null)
                {
                    PrintDebug("No burnable.");
                    return;
                }
                
                StopCooking();

                PrintDebug("Starting cooking..");
                Furnace.inventory.temperature = Furnace.cookingTemperature;
                Furnace.UpdateAttachmentTemperature();
                
                PrintDebug($"Speed Multiplier: {SpeedMultiplier}");
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