using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using DSharpPlus.VoiceNext.Codec;
using Newtonsoft.Json;

namespace DiscordBotTesting
{
    public class Program
    {
        public static DiscordClient Client;
        public static CommandsNextModule Commands;
        public static VoiceNextClient Voice;

        public static void Main(string[] args)
        {
            new Program().RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            // first, let's load our configuration file
            var json = string.Empty;
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            //TODO: overrite help to be more verbose, example in one of samples

            #region Client
            Client = new DiscordClient(new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            });

            Client.ClientErrored += Client_ClientErrored;
            #endregion

            #region Commands
            Commands = Client.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefix = cfgjson.CommandPrefix,
                EnableDms = false,
                EnableMentionPrefix = true
            });

            Commands.CommandExecuted += Commands_CommandExecuted;
            Commands.CommandErrored += Commands_CommandErrored;

            Commands.RegisterCommands<Commands>();
            CommandsNextUtilities.RegisterConverter(new CommandConverters.NullableBoolConverter());
            CommandsNextUtilities.RegisterConverter(new CommandConverters.NullableIntConverter());
            #endregion

            #region Voice
            Voice = Client.UseVoiceNext(new VoiceNextConfiguration
            {
                VoiceApplication = VoiceApplication.Music
            });
            #endregion

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        private Task Client_ClientErrored(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "ExampleBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "ExampleBot", $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'", DateTime.Now);
            return Task.CompletedTask;
        }

        private async Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Error, "ExampleBot", $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}", DateTime.Now);

            if (e.Exception is ChecksFailedException ex)
            {
                var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You do not have the permissions required to execute this command.",
                    Color = new DiscordColor(0xFF0000) // red
                };
                await e.Context.RespondAsync("", embed: embed);
            }
        }
    }

    // this structure will hold data from config.json
    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("prefix")]
        public string CommandPrefix { get; private set; }
    }
}
