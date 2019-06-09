using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Authentication", "Iv Misticos", "3.0.0")]
    [Description("Players must enter a password after they wake up or else they'll be kicked.")]
    public class Authentication : CovalencePlugin
    {
        #region Variables

        private static Oxide.Core.Libraries.Timer timer = GetLibrary<Oxide.Core.Libraries.Timer>();

        private static Authentication _ins;
	    
        #endregion
        
        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = false;
		    
            [JsonProperty(PropertyName = "Time To Authenticate")]
            public int Timeout = 20;
		    
            [JsonProperty(PropertyName = "Retries")]
            public int Retries = 2;
		    
            [JsonProperty(PropertyName = "Command")]
            public string Command = "auth";

            [JsonProperty(PropertyName = "Remember Last Authorized IP")]
            public bool RememberIP = true;

            [JsonProperty(PropertyName = "Allow Password Creation")]
            public bool AllowPasswordCreation = true;

            [JsonProperty(PropertyName = "Allow Joining Without Having Password Set")]
            public bool AllowJoiningNoPassword = true;
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

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        
        #endregion

        #region Work with Data

        private static PluginData _data = new PluginData();

        private class PluginData
        {
            public List<User> Users = new List<User>();
            
            public class User
            {
                public string ID;
                
                public string LastIP;
                
                public string Password;

                [JsonIgnore] public IPlayer Player;
                
                [JsonIgnore] public Request Request;

                public User()
                {
                    Player = _ins.players.FindPlayerById(ID);
                }

                public User Find(string id)
                {
                    foreach (var user in _data.Users)
                    {
                        if (user.ID == id)
                            return user;
                    }

                    return null;
                }
            }

            public class Request
            {
                public User User;

                public IPlayer Player => User.Player;

                public Timer Timer;

                public bool Authenticated = false;

                public int Tries = 0;

                public void TimerMethod()
                {
                    if (Authenticated)
                        return;
                    
                    if (User.Player.IsConnected)
                    {
                        Player.Kick(_ins.GetMsg("Authentication Timed Out", Player.Id));
                        return;
                    }
                }

                public void Authenticate(string password)
                {
                    var result = TryAuthenticate(password);
                    if (!result.HasValue)
                        return;

                    Player.Reply(result.Value
                        ? _ins.GetMsg("Authentication Successful", Player.Id)
                        : _ins.GetMsg("Incorrect Password", Player.Id));
                }

                public bool? TryAuthenticate(string password)
                {
                    if (password == User.Password)
                        return true;

                    if (++Tries < _config.Retries)
                        return false;
                    
                    Player.Kick(_ins.GetMsg("Authentication Exceeded Retries", Player.Id));
                    return null;
                }
            }
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
                { "Password Request", "Type /auth use [password] in the following {timeout} seconds to authenticate or you'll be kicked." },
                { "Authentication Timed Out", "You took too long to authenticate." },
                { "Authentication Exceeded Retries", "You have exceeded the maximum amount of retries." },
                { "Authentication Successful", "You have successfully authenticated." },
                { "Incorrect Password", "This password is NOT correct." },
                { "Already Authenticated", "You have already authenticated." },
                { "Plugin Disabled", "This feature is unavailable." }
            }, this);
        }
		
        private void Loaded()
        {
            permission.RegisterPermission("authentication.edit", this);
		    
            AddCovalenceCommand(_config.Command, "CommandAuth");
            
            // TODO: Iterate through all the guys on the server
        }
        
        // TODO: On player joined

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            // TODO: Block chat
            return null;
        }
        
        // TODO: Better Chat support
	    
        #endregion
		
        #region Commands
		
        private void CommandAuth(IPlayer player, string command, string[] args)
        {
            if (!_config.Enabled)
            {
                player.Reply(GetMsg("Plugin Disabled", player.Id));
                return;
            }
            
            // TODO: Switch for setting a password, authenticating, etc
        }
		
        #endregion
		
        #region Helpers

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);
		
        #endregion
    }
}