using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Cash System", "Iv Misticos", "1.0.0")]
    [Description("Rich economics system")]
    class CashSystem : RustPlugin
    {
        #region Variables

        private static PluginData _data;
        
        #endregion
        
        #region Configuration
        
        private Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Currency List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Currency> Currencies = new List<Currency>
            {
                new Currency()
            };
        }

        public class Currency
        {
            [JsonProperty(PropertyName = "Abbreviation")]
            public string Abbreviation = "$";
            
            [JsonProperty(PropertyName = "Start Amount")]
            public float StartAmount = 0f;
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

        private class PluginData
        {
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PlayerData> Players = new List<PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty(PropertyName = "SteamID")]
            public ulong Id = 0;
            
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CurrencyData> Currencies = new List<CurrencyData>();

            public static PlayerData Find(ulong id)
            {
                for (var i = 0; i < _data.Players.Count; i++)
                {
                    var data = _data.Players[i];
                    if (data.Id == id)
                        return data;
                }

                return null;
            }

            public CurrencyData FindCurrency(string abbreviation)
            {
                for (var i = 0; i < Currencies.Count; i++)
                {
                    var data = Currencies[i];
                    if (data.Abbreviation == abbreviation)
                        return data;
                }

                return null;
            }
        }

        private class CurrencyData
        {
            public string Abbreviation = "$";

            [JsonIgnore] public Currency Currency = null;

            [JsonIgnore] public float Balance = 0f;
            
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TransactionData> Transactions = new List<TransactionData>();

            public void Add(float amount, string description)
            {
                var transaction = new TransactionData
                {
                    Amount = amount,
                    Description = description
                };
                
                Transactions.Add(transaction);
                Balance += amount;
            }
        }

        private class TransactionData
        {
            public float Amount = 0f;

            public string Description = string.Empty;
        }

        #endregion
        
        #region Hooks

        private void Loaded()
        {
            for (var i = 0; i < _data.Players.Count; i++)
            {
                var player = _data.Players[i];
                for (var j = 0; j < player.Currencies.Count; j++)
                {
                    var currency = player.Currencies[j];
                    for (var k = 0; k < currency.Transactions.Count; k++)
                    {
                        currency.Balance += currency.Transactions[i].Amount;
                    }
                }
            }
        }
        
        #endregion
        
        #region API

        private List<string> API_GetCurrencies(ulong id)
        {
            var player = PlayerData.Find(id);
            if (player == null)
                return null;

            var data = new List<string>();
            for (var i = 0; i < player.Currencies.Count; i++)
            {
                data.Add(player.Currencies[i].Abbreviation);
            }

            return data;
        }

        private float API_GetBalance(ulong id, string currency)
        {
            var player = PlayerData.Find(id);
            var data = player?.FindCurrency(currency);
            return data?.Balance ?? float.NaN; // Yeah it could be just a one line but I made it bigger for u
        }

        private bool API_AddTransaction(ulong id, string currency, float amount, string description)
        {
            var player = PlayerData.Find(id);
            var data = player?.FindCurrency(currency);
            if (data == null)
                return false;
            
            data.Add(amount, description);
            return true;
        }
        
        #endregion
    }
}