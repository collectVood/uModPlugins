using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Auto Purge", "Fujikura/Norn", "2.0.0")]
    [Description("Remove entities if the owner becomes inactive")]
    public class AutoPurge : RustPlugin
    {
	    #region Configuration

	    private static Configuration _config;

	    private class Configuration
	    {
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

	    protected override void SaveConfig() => Config.WriteObject(_config);

	    protected override void LoadDefaultConfig() => _config = new Configuration();
        
	    #endregion
	}
}