using System;
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
            // Make sure to always return things to their vanilla state
            foreach(var oven in UnityEngine.Object.FindObjectsOfType<BaseOven>())
            {
                oven.allowByproductCreation = true;

                if (oven.IsOn())
                {
                    // Stop the modded smelting, resume the vanilla smelting
                    StopCooking(oven);
                    oven.StartCooking();
                }

                // Get rid of those monobehaviors
                UnityEngine.Object.Destroy(oven.GetComponent<FurnaceController>());
            }
        }

        private void OnServerInitialized()
        {
            _instance = this;
            permission.RegisterPermission(_config.Permission, this);
            
            foreach (var oven in UnityEngine.Object.FindObjectsOfType<BaseOven>())
            {
                if (!oven.IsOn())
                    continue;
                
                NextFrame(() =>
                {
                    if (oven == null || oven.IsDestroyed)
                        return;
                    
                    // Invokes are actually removed at the end of a frame, meaning you can get multiple invokes at once after reloading plugin
                    StopCooking(oven);
                    StartCooking(oven);
                });
            }
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven is BaseFuelLightSource || oven.needsBuildingPrivilegeToUse && !player.CanBuild())
                return null;

            if (oven.IsOn())
                StopCooking(oven);
            else
                StartCooking(oven);
            
            return false;
        }
        
        #endregion
        
        #region Helpers

        
        
        #endregion
		
        public class FurnaceController : MonoBehaviour
        {
            private BaseOven _oven;
            public BaseOven Furnace
            {
                get
                {
                    if (_oven == null)
                        _oven = GetComponent<BaseOven>();

                    return _oven;
                }
            }

            private void Destroy()
            { 
                // TODO
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
                    Furnace.StopCooking();
                    return;
                }
                
                Furnace.inventory.OnCycle(0.5f);
                var slot = Furnace.GetSlot(BaseEntity.Slot.FireMod);
                if (slot)
                {
                    slot.SendMessage("Cook", 0.5f, SendMessageOptions.DontRequireReceiver);
                }
                
                var component = item.info.GetComponent<ItemModBurnable>();
                item.fuel -= 0.5f * (Furnace.cookingTemperature / 200f);
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
                    var item = ItemManager.Create(burnable.byproductItem, burnable.byproductAmount);
                    if (!item.MoveToContainer(Furnace.inventory))
                    {
                        Furnace.OvenFull();
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
        }
    }
}