using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Facepunch.Extend;
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
        private static MethodInfo _consumeFuelMethod;

        private const string PermAllow = "quicksmelt.allow";

        public List<string> FurnaceTypes = new List<string>();

        private static HashSet<string> _rawMeatNames = new HashSet<string>
        {
            "bearmeat",
            "meat.boar",
            "wolfmeat.raw",
            "humanmeat.raw",
            "fish.raw",
            "chicken.raw",
            "deermeat.raw",
            "horsemeat.raw"
        };
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            public int SmeltSpeed = 1;
            public int WoodRate = 1;
            public float CharcoalRate = 0.70f;
            public bool CanCookFoodInFurnace;
            public bool UsePermissions = false;

            [JsonProperty(PropertyName = "Large Furnace Multiplier")]
            public float LargeFurnaceMultiplier = 1.0f;
            [JsonProperty(PropertyName = "Campfire Multiplier")]
            public float CampFireMultiplier = 1.0f;
            [JsonProperty(PropertyName = "Oil Refinery Multiplier")]
            public float OilRefineryMultiplier = 1.0f;
            [JsonProperty(PropertyName = "Water Purifier Multiplier")]
            public float WaterPurifierMultiplier = 1.0f;
            //public float Efficency = 1f;
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

        private void Loaded()
        {
            _instance = this;
            permission.RegisterPermission(PermAllow, this);
            _consumeFuelMethod = typeof(BaseOven).GetMethod("ConsumeFuel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        private void Unload()
        {
            //Make sure to always return things to their vanilla state
            foreach(var oven in BaseNetworkable.serverEntities.OfType<BaseOven>())
            {
                oven.allowByproductCreation = true;

                var data = oven.GetComponent<FurnaceData>();

                if (oven.IsOn())
                {
                    //Stop the modded smelting, resume the vanilla smelting
                    StopCooking(oven);
                    oven.StartCooking();
                }

                //Get rid of those monobehaviors
                UnityEngine.Object.Destroy(data);
            }
        }

        private void OnServerInitialized()
        {
            foreach(var oven in BaseNetworkable.serverEntities.OfType<BaseOven>().Where(x=>x.IsOn()))
            {
                NextFrame(() =>
                {
                    if (oven == null || oven.IsDestroyed)
                    {
                        return;
                    }
                    //So invokes are actually removed at the end of a frame, meaning you can get multiple invokes at once after reloading plugin
                    StopCooking(oven);
                    StartCooking(oven);
                });
            }
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven is BaseFuelLightSource || (oven.needsBuildingPrivilegeToUse && !player.CanBuild()))
            {
                return null;
            }
            if (!oven.HasFlag(BaseEntity.Flags.On))
            {
                StartCooking(oven);
            }
            else
            {
                StopCooking(oven);
            }
            return false;
        }
        
        #endregion
        
        #region Helpers
        
        public Item FindBurnable(BaseOven oven)
        {
            return oven.inventory.itemList.FirstOrDefault(x => //TIL you can declare Linq over multiple lines
            {
                var comp = x.info.GetComponent<ItemModBurnable>();
                if (comp != null && (oven.fuelType == null || x.info == oven.fuelType))
                {
                    return true;
                }
                return false;
            });
        }
        
        //Overwriting Oven.StartCooking
        private void StartCooking(BaseOven oven)
        {
            if ((_config.UsePermissions && !permission.UserHasPermission(oven.OwnerID.ToString(), PermAllow)))
            {
                oven.StartCooking();
                return;
            }
            if (FindBurnable(oven) == null)
            {
                return;
            }
            oven.UpdateAttachmentTemperature();
            var data = oven.transform.GetOrAddComponent<FurnaceData>();
            oven.CancelInvoke(oven.Cook);
            oven.InvokeRepeating(data.CookOverride, 0.5f, 0.5f);
            oven.SetFlag(BaseEntity.Flags.On, true);
        }
        
        private void StopCooking(BaseOven oven)
        {
            var data = oven.transform.GetOrAddComponent<FurnaceData>();
            oven.CancelInvoke(data.CookOverride);
            oven.StopCooking();
        }
        
        private void OnConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            var slot = oven.GetSlot(BaseEntity.Slot.FireMod);
            if (slot != null)
            {
                //Usually 0.5f for cook tick, fuel is only consumed twice per second
                slot.SendMessage("Cook", 2f * _config.SmeltSpeed * _config.WaterPurifierMultiplier, SendMessageOptions.DontRequireReceiver);
            }
        
            if (oven == null || oven is BaseFuelLightSource)
            {
                return;
            }
        
            // Check if permissions are enabled and player has permission
            if (_config.UsePermissions && !permission.UserHasPermission(oven.OwnerID.ToString(), PermAllow)) return;
        
            var data = oven.transform.GetOrAddComponent<FurnaceData>();
        
            #region Charcoal Modifier
        
            if (burnable.byproductItem != null)
            {
                oven.allowByproductCreation = false;
        
                var charcoalAmount = 0;
        
                var modifiedRate = _config.CharcoalRate * _config.WoodRate;
                        
                charcoalAmount += (int)(_config.CharcoalRate * _config.WoodRate);
        
                modifiedRate -= charcoalAmount;
        
                if (modifiedRate > 0 && modifiedRate <= 1f)
                {
                    if (Random.Range(0f, 1f) < modifiedRate)
                    {
                        charcoalAmount += 1;
                    }
                }
        
                if (charcoalAmount > 0)
                {
                    TryAddItem(oven.inventory, burnable.byproductItem, Mathf.Min(charcoalAmount, fuel.amount));
                }
            }
        
            #endregion
        
            // Modify the amount of fuel to use
            fuel.UseItem(_config.WoodRate - 1);
        }
        
        public static int TakeFromInventorySlot(ItemContainer container, int itemId, int amount, Item item)
        {
            if (item.info.itemid != itemId) return 0;
        
            if (item.amount > amount)
            {
                item.UseItem(amount);
                return amount;
            }
        
            amount = item.amount;
            item.Remove();
            return amount;
        }
        
        public static void TryAddItem(ItemContainer container, ItemDefinition definition, int amount)
        {
            var amountLeft = amount;
            foreach (var item in container.itemList)
            {
                if (item.info != definition)
                {
                    continue;
                }
                if (amountLeft <= 0)
                {
                    return;
                }
                if (item.amount < item.MaxStackable())
                {
                    var amountToAdd = Mathf.Min(amountLeft, item.MaxStackable() - item.amount);
                    item.amount += amountToAdd;
                    item.MarkDirty();
                    amountLeft -= amountToAdd;
                }
            }
            if (amountLeft <= 0)
            {
                return;
            }
            var smeltedItem = ItemManager.Create(definition, amountLeft);
            if (!smeltedItem.MoveToContainer(container))
            {
                smeltedItem.Drop(container.dropPosition, container.dropVelocity);
                var oven = container.entityOwner as BaseOven;
                if (oven != null)
                {
                    _instance.StopCooking(oven);
                    //oven.OvenFull();
                }
            }
        }
        
        private void ConsumeFuel(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            Interface.CallHook("OnConsumeFuel", oven, fuel, burnable);
            if (oven.allowByproductCreation && burnable.byproductItem != null && Random.Range(0.0f, 1f) > (double) burnable.byproductChance)
            {
                var obj = ItemManager.Create(burnable.byproductItem, burnable.byproductAmount);
                if (!obj.MoveToContainer(oven.inventory))
                {
                    oven.OvenFull();
                    obj.Drop(oven.inventory.dropPosition, oven.inventory.dropVelocity);
                }
            }
            if (fuel.amount <= 1)
            {
                fuel.Remove();
            }
            else
            {
                --fuel.amount;
                fuel.fuel = burnable.fuelAmount;
                fuel.MarkDirty();
            }
        }
        
        #endregion
		
        

        public class FurnaceData : MonoBehaviour
        {
            private BaseOven _oven;
            public BaseOven Furnace { get { if (_oven == null) { _oven = GetComponent<BaseOven>(); } return _oven; } } //One line bullshit right here

            public int SmeltTicks;
            public Dictionary<string, float> ItemLeftovers = new Dictionary<string, float>();

            public void CookOverride()
            {
                SmeltTicks++;
                if (SmeltTicks % 2 == 0)
                {
                    TrySmeltItems();
                }
                var burnable = _instance.FindBurnable(Furnace);
                if (burnable == null)
                {
                    _instance.StopCooking(Furnace);
                    return;
                }
                var component = burnable.info.GetComponent<ItemModBurnable>();
                burnable.fuel -= 0.5f * Furnace.cookingTemperature / 200f;
                if (!burnable.HasFlag(global::Item.Flag.OnFire))
                {
                    burnable.SetFlag(global::Item.Flag.OnFire, true);
                    burnable.MarkDirty();
                }
                if (burnable.fuel <= 0f)
                {
                    var array = ArrayPool.Get(2);
                    array[0] = burnable;
                    array[1] = component;
                    _consumeFuelMethod.Invoke(Furnace, array);
                    ArrayPool.Free(array);
                }
            }

            private void TrySmeltItems()
            {
                #region Smelt Modifier

                int smeltLoops = Mathf.Max(1, _config.GetSmeltRate(Furnace));

                if (smeltLoops > 0)
                {
                    //Took from QuickSmelt and modified
                    // Loop through furance inventory slots
                    for (var i = 0; i < Furnace.inventory.itemList.Count; i++)
                    {
                        // Check for and ignore invalid items
                        var slotItem = Furnace.inventory.itemList[i];
                        if (slotItem == null || !slotItem.IsValid())
                        {
                            continue;
                        }

                        // Check for and ignore non-cookables
                        var cookable = slotItem.info.GetComponent<ItemModCookable>();
                        if (cookable == null)
                        {
                            continue;
                        }

                        //Make sure oil refinery only cooks oil, fireplace cooks food, furnace cooks ore
                        if (cookable.lowTemp > Furnace.cookingTemperature || cookable.highTemp < Furnace.cookingTemperature)
                        {
                            if (_config.CanCookFoodInFurnace)
                            {
                                //Allow food to be cooked in furnaces
                                if (!_rawMeatNames.Contains(slotItem.info.shortname))
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }

                        //_plugin.Puts($"{SmeltTicks} {(int)(cookable.cookTime)}");

                        //We do probability over time to see if we should cook instead of keeping track of item smelt time
                        //Will change this back to linear smelting once we expose ItemModCookable.OnCycle()
                        if ((int)cookable.cookTime != 0 && (SmeltTicks / 2) % (int)(cookable.cookTime) != 0)
                        {
                            continue;
                        }

                        // Skip already cooked food items
                        if (slotItem.info.shortname.EndsWith(".cooked"))
                        {
                            continue;
                        }

                        // Set consumption to however many we can pull from this actual stack
                        var consumptionAmount = TakeFromInventorySlot(Furnace.inventory, slotItem.info.itemid, _config.SmeltSpeed, slotItem);

                        // If we took nothing, then... we can't create any
                        if (consumptionAmount <= 0)
                        {
                            continue;
                        }

                        //Will be used for efficency
                        var extraLoops = 1;

                        // Create the item(s) that are now cooked
                        TryAddItem(Furnace.inventory, cookable.becomeOnCooked, (cookable.amountOfBecome * consumptionAmount * extraLoops));
                    }
                }

                #endregion

                ItemManager.DoRemoves();
            }

            private void Destroy()
            { 
                Furnace.CancelInvoke(CookOverride);
            }
        }
        
        [ConsoleCommand("quicksmelt")]
        private void QuickSmeltInfoCommand(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin)
            {
                return;
            }
            var table = new TextTable();
            table.AddColumns("Description", "Setting", "Console Command");
            table.AddRow("", "");
            table.AddRow("Smelt Speed", $"{_config.SmeltSpeed}x", "quicksmelt.smelt");
            table.AddRow("Charcoal Rate", $"{_config.CharcoalRate:0.0}x", "quicksmelt.charcoal");
            table.AddRow("Wood Rate", $"{_config.WoodRate}x", "quicksmelt.wood");
            table.AddRow("Will Food Cook In Furnace", $"{_config.CanCookFoodInFurnace}", "quicksmelt.food");
            args.ReplyWith(table.ToString());
        }
    }
}