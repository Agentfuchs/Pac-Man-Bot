using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;
using PacManBot.Utils;


// Made by Samrux for fun
// GitHub repo: https://github.com/Samrux/Pac-Man-Bot

namespace PacManBot
{
    /// <summary>
    /// Программа для настройки и запуска бота
    /// </summary>
    public static class Program
    {
        /// <summary>The bot program's displayed version.</summary>
        public static readonly string Version = Assembly.GetEntryAssembly().GetName().Version.ToString().TrimEnd(".0");

        /// <summary>The random number generator used throughout the program.</summary>
        public static readonly ConcurrentRandom Random = new ConcurrentRandom();


        public static Task Main(string[] args)
        {
            // Load configuration
            foreach (string requiredFile in new[] { Files.Config, Files.Contents })
                if (!File.Exists(requiredFile))
                    throw new InvalidOperationException($"Отсутствует необходимый {requiredFile}: Бот не может запустится.");

            BotConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText(Files.Config));
                config.LoadContent(File.ReadAllText(Files.Contents));
            }
            catch (JsonReaderException e)
            {
                throw new InvalidOperationException("Требуемый файл содержит недопустимый JSON. Исправьте ошибку и попробуйте еще раз.", e);
            }

            if (string.IsNullOrWhiteSpace(config.discordToken))
                throw new InvalidOperationException($"Отсутствует {nameof(config.discordToken)} в {Files.Config}: Бот не может запустится.");


            // Настраивает и запускает бота
            var log = new LoggingService(config);
            log.Critical($"Pac-Man бот v{Version}");

            return Host.CreateDefaultBuilder(args)
                .ConfigureLogging(lb => lb.ClearProviders().AddProvider(log))
                .ConfigureServices(services => services
                    .AddHostedService<Bot>()
                    .AddSingleton<DiscordShardedClient>()
                    .AddSingleton<DatabaseService>()
                    .AddSingleton<InputService>()
                    .AddSingleton<GameService>()
                    .AddSingleton<SchedulingService>()
                    .AddSingleton(log)
                    .AddSingleton(config)
                    .AddSingleton(config.MakeDiscordConfig(log)))
                .RunConsoleAsync();
        }
    }
}
