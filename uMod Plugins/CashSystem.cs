using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Cash System", "Iv Misticos", "1.0.3")]
    [Description("Rich economics system")]
    class CashSystem : CovalencePlugin
    {
        #region Variables

        private static CashSystem _ins;

        private static PluginData _data;

        private static Time _time = GetLibrary<Time>();
        
        #endregion
        
        #region Configuration
        
        private static Configuration _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Currency List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Currency> Currencies = new List<Currency>
            {
                new Currency()
            };

            [JsonProperty(PropertyName = "Purge Old Data")]
            public bool Purge = true;

            [JsonProperty(PropertyName = "Time Between Latest Update And Purge")]
            public uint PurgeTime = 604800;
        }

        private class Currency
        {
            [JsonProperty(PropertyName = "Abbreviation")]
            public string Abbreviation = "$";
            
            [JsonProperty(PropertyName = "Start Amount")]
            public double StartAmount = 0f;
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
            public string Id;
            
            [JsonProperty(PropertyName = "Last Update")]
            public uint LastUpdate = _time.GetUnixTimestamp();
            
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CurrencyData> Currencies = new List<CurrencyData>();

            public static PlayerData Find(string id)
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

            public void UpdateCurrencies()
            {
                for (var i = 0; i < _config.Currencies.Count; i++)
                {
                    var currency = _config.Currencies[i];
                    var foundCurrency = FindCurrency(currency.Abbreviation);
                    if (foundCurrency == null)
                    {
                        foundCurrency = new CurrencyData
                        {
                            Abbreviation = currency.Abbreviation
                        };

                        foundCurrency.Add(currency.StartAmount, GetMsg("Start Amount Transfer", Id));
                        Currencies.Add(foundCurrency);
                    }
                
                    foundCurrency.RecalculateBalance();
                }
            }

            public void Update()
            {
                LastUpdate = _time.GetUnixTimestamp();
            }
        }

        private class CurrencyData
        {
            public string Abbreviation = "$";

            [JsonIgnore] public double Balance;
            
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TransactionData> Transactions = new List<TransactionData>();

            public void Add(double amount, string description)
            {
                var transaction = new TransactionData
                {
                    Amount = amount,
                    Description = description
                };
                
                Transactions.Add(transaction);
                Balance += amount;
            }

            public void RecalculateBalance()
            {
                Balance = 0d;
                for (var i = 0; i < Transactions.Count; i++)
                {
                    Balance += Transactions[i].Amount;
                }
            }
        }

        private class TransactionData
        {
            public double Amount;

            // ReSharper disable once NotAccessedField.Local
            public string Description = string.Empty;

            public uint Timestamp = _time.GetUnixTimestamp();
        }

        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Start Amount Transfer", "Start Amount" },
//                { "Not Enough Permissions", "You don't have enough permissions" },
//                { "Admin Money Transfer", "Admin money transfer" },
//                { "Incorrect Number", "Incorrect number" },
//                { "Balance Get Error", "There was an error while getting this player's balance. Check his ID and the currency twice." },
//                { "Balance Get", "Balance: {balance}" },
//                { "Balance Add Success", "Success. Balance changed!" },
//                { "Balance Add Error", "There was an error." },
//                { "Balance Help", "Help:\n" +
//                                  "get ID Currency - Get player balance\n" +
//                                  "add ID Currency Amount - Add balance" },
            }, this);
        }

        private void Loaded()
        {
            _ins = this;
            
            // Loading, purging, saving

            LoadData();

            if (_config.Purge)
            {
                var currentTime = _time.GetUnixTimestamp();
                for (var i = _data.Players.Count - 1; i >= 0; i--)
                {
                    var data = _data.Players[i];
                    if (data.LastUpdate + _config.PurgeTime < currentTime)
                    {
                        _data.Players.RemoveAt(i);
                        continue;
                    }

                    data.UpdateCurrencies();
                }
            }
            
            SaveData();
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        private void OnUserConnected(IPlayer player)
        {
            var data = PlayerData.Find(player.Id);
            if (data == null)
            {
                data = new PlayerData
                {
                    Id = player.Id
                };
                
                _data.Players.Add(data);
            }
            
            data.UpdateCurrencies();
            data.Update();
        }
        
        #endregion
        
//        #region Commands
//
//        [Command("balance")]
//        private void CommandBalance(IPlayer player, string command, string[] args)
//        {
//            if (!player.IsAdmin)
//            {
//                player.Reply(GetMsg("Not Enough Permissions", player.Id));
//                return;
//            }
//
//            if (args.Length <= 2)
//            {
//                player.Reply(GetMsg("Balance Help", player.Id));
//                return;
//            }
//
//            switch (args[0])
//            {
//                case "get":
//                {
//                    var balance = GetBalance(args[1], args[2]);
//                    if (double.IsNaN(balance))
//                    {
//                        player.Reply(GetMsg("Balance Get Error", player.Id));
//                        return;
//                    }
//                    
//                    player.Reply(GetMsg("Balance Get", player.Id).Replace("{balance}", balance.ToString(CultureInfo.CurrentCulture)));
//                    break;
//                }
//
//                case "add":
//                {
//                    if (args.Length <= 3)
//                    {
//                        player.Reply(GetMsg("Balance Help", player.Id));
//                        return;
//                    }
//
//                    double amount;
//                    if (!double.TryParse(args[3], out amount))
//                    {
//                        player.Reply(GetMsg("Incorrect Number", player.Id));
//                        return;
//                    }
//
//                    var success = AddTransaction(args[1], args[2], amount, GetMsg("Admin Money Transfer", args[1]));
//                    if (success)
//                    {
//                        player.Reply(GetMsg("Balance Add Success", player.Id));
//                        return;
//                    }
//
//                    player.Reply(GetMsg("Balance Add Error", player.Id));
//                    break;
//                }
//            }
//        }
//        
//        #endregion
        
        #region API

        private List<string> GetCurrencies(string id)
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

        private List<string> GetCurrencies()
        {
            var data = new List<string>();
            for (var i = 0; i < _config.Currencies.Count; i++)
            {
                data.Add(_config.Currencies[i].Abbreviation);
            }

            return data;
        }

        private double GetBalance(string id, string currency)
        {
            var player = PlayerData.Find(id);
            var data = player?.FindCurrency(currency);
            return data?.Balance ?? double.NaN; // Yeah it could be just a one line but I made it bigger for u
        }

        private bool AddTransaction(string id, string currency, double amount, string description)
        {
            var player = PlayerData.Find(id);
            var data = player?.FindCurrency(currency);
            if (data == null)
                return false;
            
            data.Add(amount, description);
            player.Update();
            return true;
        }

        private JObject GetTransactions(string id, string currency)
        {
            var player = PlayerData.Find(id);
            var data = player?.FindCurrency(currency);
            return data == null ? null : JObject.FromObject(data.Transactions);
        }
        
        #endregion
        
        #region Helpers

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        #endregion
    }
}