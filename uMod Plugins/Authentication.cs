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
	    
	    
	    
	    #endregion
        
	    #region Configuration

	    private static Configuration _config;

	    public class Configuration
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

		    [JsonProperty(PropertyName = "Enable Password Creation")]
		    public bool EnablePasswordCreation = true;

		    [JsonProperty(PropertyName = "Allow Joining With Password Disabled")]
		    public bool AllowJoiningUnregistered = true;
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
	    
	    #region Hooks
		
	    protected override void LoadDefaultMessages()
	    {
		    lang.RegisterMessages(new Dictionary<string, string>
		    {
			    {
				    "Password Request",
				    "Type /auth [password] in the following {timeout} seconds to authenticate or you'll be kicked."
			    },
			    {
				    "Authentication Timed Out",
				    "You took too long to authenticate or exceeded the maximum amount of retries."
			    },
			    {
				    "Authentication Successful",
				    "Authentication successful."
					
			    }
		    }, this);
	    }
		
	    private void Loaded()
	    {
		    permission.RegisterPermission("authentication.edit", this);
		    
		    AddCovalenceCommand(_config.Command, "CommandAuth");
		    
//		    var online = BasePlayer.activePlayerList;
//		    foreach(var player in online)
//		    {
//			    var request = new Request(player.UserIDString, player);
//			    request.MAuthenticated = true;
//			    _requests.Add(request);
//		    }
	    }

	    private object OnPlayerChat(ConsoleSystem.Arg arg)
	    {
			// TODO
		    return null;
	    }
	    
	    #endregion
		
		#region Commands
		
		private void CommandAuth(IPlayer player, string command, string[] args)
		{
			
		}
		
		#endregion
		
		#region Helpers

		private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);
		
		#endregion
    }
}