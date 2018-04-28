using System;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.Commands;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace PacManBot.Services
{
    public class ScriptingService // Thanks to oatmeal and amibu
    {
        private readonly ScriptOptions scriptOptions;
        private readonly StorageService storage;
        private readonly LoggingService logger;


        public ScriptingService(StorageService storage, LoggingService logger)
        {
            this.storage = storage;
            this.logger = logger;

            scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System", "System.IO", "System.Threading.Tasks", "System.Collections.Generic", "System.Linq", "System.Text.RegularExpressions",
                    "Discord", "Discord.WebSocket", "Discord.Commands",
                    "PacManBot", "PacManBot.Services", "PacManBot.Constants", "PacManBot.Utils", "PacManBot.Modules", "PacManBot.Modules.PacMan"
                )
                .WithReferences(
                    typeof(SocketCommandContext).Assembly,
                    typeof(StorageService).Assembly,
                    typeof(IMessageChannel).Assembly,
                    typeof(RestUserMessage).Assembly
                );
        }


        public async Task Eval(string code, SocketCommandContext context)
        {
            try
            {
                string postCode = "\nTask ReplyAsync(string msg) => Context.Channel.SendMessageAsync(msg);";

                code = code.Trim(' ', '`', '\n');
                if (code.StartsWith("cs\n")) code = code.Remove(0, 3); //C# code block in Discord

                if (!code.Contains(";")) //Treats a single expression as a message to send in chat
                {
                    code = "await ReplyAsync($\"{" + code + "}\");";
                }

                await CSharpScript.EvaluateAsync(code + postCode, scriptOptions, new ScriptArgs(context, storage, logger));
                await logger.Log(LogSeverity.Info, $"Successfully executed code in channel {context.Channel.FullName()}:\n{code}");
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }



    public class ScriptArgs : EventArgs
    {
        public readonly SocketCommandContext Context;
        public readonly StorageService storage;
        public readonly LoggingService logger;

        public ScriptArgs(SocketCommandContext Context, StorageService storage, LoggingService logger)
        {
            this.Context = Context;
            this.storage = storage;
            this.logger = logger;
        }
    }
}
