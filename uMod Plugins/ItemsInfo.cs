using System;
using System.Collections.Generic;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Items Info", "Iv Misticos", "1.0.2")]
    [Description("Get actual information about items.")]
    class ItemsInfo : RustPlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Incorrect Arguments", "Please, specify correct arguments." }
            }, this);
        }

        // ReSharper disable once UnusedMember.Local
        private void Init()
        {
            cmd.AddConsoleCommand("itemsinfo.all", this, CommandConsoleHandle);
            cmd.AddConsoleCommand("itemsinfo.find", this, CommandConsoleHandle);
        }

        private bool CommandConsoleHandle(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon && !arg.IsConnectionAdmin)
                return false;
            
            arg.ReplyWith(GetItemsInfo(arg.Args, arg.cmd.FullName == "itemsinfo.find"));
            return true;
        }

        private string GetItemsInfo(string[] parameters, bool search)
        {
            if (parameters == null || (search && parameters.Length < 2) || parameters.Length < 1)
                return GetMsg("Incorrect Arguments");
            
            var reply = new StringBuilder();
            var items = ItemManager.itemList;
            var itemsCount = items.Count;

            var found = 0;
            for (var i = 0; i < itemsCount; i++)
            {
                var item = items[i];
                if (search && item.shortname.IndexOf(parameters[0], StringComparison.CurrentCultureIgnoreCase) == -1)
                    continue;

                for (var j = search ? 1 : 0; j < parameters.Length; j++)
                {
                    switch (parameters[j])
                    {
                        case "number":
                        {
                            reply.Append($"#{++found}\n");
                            break;
                        }
                        
                        case "shortname":
                        {
                            reply.Append($"Shortname: {item.shortname}\n");
                            break;
                        }

                        case "id":
                        {
                            reply.Append($"ID: {item.itemid}\n");
                            break;
                        }

                        case "name":
                        {
                            reply.Append($"Name: {item.displayName.english}\n");
                            break;
                        }

                        case "description":
                        {
                            reply.Append($"Description: {item.displayDescription.english}\n");
                            break;
                        }

                        case "condition":
                        {
                            reply.Append($"Max Condition: {item.condition.max}\n");
                            break;
                        }

                        case "repair":
                        {
                            reply.Append($"Is Repairable: {item.condition.repairable}\n");
                            break;
                        }
                    }
                }
            }

            return reply.ToString();
        }

        private string GetMsg(string key) => lang.GetMessage(key, this);
    }
}