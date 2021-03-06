using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DiscordBotsList.Api;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using PacManBot.Commands;
using Microsoft.Extensions.Hosting;
using System.Threading;
using DSharpPlus.CommandsNext.Converters;
using System.Collections.Concurrent;

namespace PacManBot
{
    /// <summary>
    /// Runs an instance of Pac-Man Bot and handles its connection to Discord.
    /// </summary>
    public class Bot : IHostedService
    {
        private readonly BotConfig _config;
        private readonly IServiceProvider _services;

        private readonly DiscordShardedClient _client;
        private readonly LoggingService _log;
        private readonly GameService _games;
        private readonly InputService _input;
        private readonly SchedulingService _schedule;

        private readonly ConcurrentDictionary<int, DiscordClient> _shardsReady = new();
        private AuthDiscordBotListApi _discordBotList = null;
        private DateTime _lastGuildCountUpdate = DateTime.MinValue;
        private bool _clientStarted = false;


        public Bot(BotConfig config, IServiceProvider services, DiscordShardedClient client,
            LoggingService log, GameService games, InputService input, SchedulingService schedule)
        {
            _config = config;
            _services = services;
            _client = client;
            _log = log;
            _games = games;
            _input = input;
            _schedule = schedule;
        }


        /// <summary>Starts the bot and its connection to Discord.</summary>
        public async Task StartAsync(CancellationToken ct)
        {
            await _client.UseCommandsNextAsync(new CommandsNextConfiguration
            {
                UseDefaultCommandHandler = false,
                Services = _services,
            });
            foreach (var (shard, commands) in await _client.GetCommandsNextAsync())
            {
                commands.RegisterCommands(typeof(BaseModule).Assembly);
                commands.RegisterConverter(new EnumConverter<ActivityType>());
                commands.RegisterConverter(new EnumConverter<UserStatus>());
                commands.SetHelpFormatter<HelpFormatter>();
            }

            await _games.LoadGamesAsync();

            _client.Ready += OnReady;
            _client.ClientErrored += OnClientErrored;
            _client.SocketErrored += OnSocketErrored;
            _client.GuildCreated += OnJoinedGuild;
            _client.GuildDeleted += OnLeftGuild;
            _client.ChannelDeleted += OnChannelDeleted;

            if (ct.IsCancellationRequested) return;

            await _client.StartAsync();
            _clientStarted = true;

            if (!string.IsNullOrWhiteSpace(_config.discordBotListToken))
            {
                _discordBotList = new AuthDiscordBotListApi(_client.CurrentUser.Id, _config.discordBotListToken);
            }
        }


        /// <summary>Safely stop most activity from the bot and disconnect from Discord.</summary>
        public async Task StopAsync(CancellationToken ct)
        {
            _log.Info("Отключение бота");
            
            foreach (var shard in _client.ShardClients.Values)
                _input.StopListening(shard);

            _schedule.StopTimers();

            _client.Ready -= OnReady;
            _client.ClientErrored -= OnClientErrored;
            _client.SocketErrored -= OnSocketErrored;
            _client.GuildCreated -= OnJoinedGuild;
            _client.GuildDeleted -= OnLeftGuild;
            _client.ChannelDeleted -= OnChannelDeleted;

            if (_clientStarted)
            {
                await Task.WhenAny(_client.StopAsync(), Task.Delay(5000, CancellationToken.None));
            }
        }




        private Task OnClientErrored(DiscordClient shard, ClientErrorEventArgs args)
        {
            _log.Exception($"В {args.EventName} обработчик", args.Exception);
            return Task.CompletedTask;
        }


        private Task OnSocketErrored(DiscordClient shard, SocketErrorEventArgs args)
        {
            _log.Warning($"В части {shard.ShardId} - {args.Exception.GetType().Name}: {args.Exception.Message}");
            return Task.CompletedTask;
        }


        private Task OnReady(DiscordClient shard, ReadyEventArgs args)
        {
            _ = Task.Run(() => OnReadyAsync(shard).LogExceptions(_log, $"Готово {shard.ShardId}"));
            return Task.CompletedTask;
        }


        private async Task OnReadyAsync(DiscordClient shard)
        {
            if (_shardsReady.ContainsKey(shard.ShardId)) return;

            _shardsReady.TryAdd(shard.ShardId, shard);

            if (_shardsReady.Count <= shard.ShardCount)
            {
                _input.StartListening(shard);
            }
            if (_shardsReady.Count == shard.ShardCount)
            {
                _schedule.StartTimers();
                await _client.UpdateStatusAsync(
                    new DiscordActivity($"пакмена!", ActivityType.Playing), UserStatus.Online, DateTime.Now);
                _log.Info($"Все части готовы");
            }

            if (File.Exists(Files.ManualRestart))
            {
                try
                {
                    ulong[] id = File.ReadAllText(Files.ManualRestart)
                    .Split("/").Select(ulong.Parse).ToArray();

                    var channel = await shard.GetChannelAsync(id[0]);
                    if (channel is not null)
                    {
                        File.Delete(Files.ManualRestart);
                        var message = await channel.GetMessageAsync(id[1]);
                        if (message is not null) await message.ModifyAsync(CustomEmoji.Check);
                        _log.Info("Возобновлено после ручного перезапуска");
                    }
                }
                catch (Exception e)
                {
                    _log.Exception("После ручного перезапуска", e);
                    if (File.Exists(Files.ManualRestart) && e is not IOException) File.Delete(Files.ManualRestart);
                }
            }
        }


        private Task OnJoinedGuild(DiscordClient shard, GuildCreateEventArgs args)
        {
            _ = Task.Run(() => UpdateGuildCountAsync().LogExceptions(_log, "Обновление количества серверов"));
            return Task.CompletedTask;
        }


        private Task OnLeftGuild(DiscordClient shard, GuildDeleteEventArgs args)
        {
            foreach (var channel in args.Guild.Channels.Keys)
            {
                _games.Remove(_games.GetForChannel(channel));
            }
            return Task.CompletedTask;
        }


        private Task OnChannelDeleted(DiscordClient shard, ChannelDeleteEventArgs args)
        {
            _games.Remove(_games.GetForChannel(args.Channel.Id));
            return Task.CompletedTask;
        }


        private async Task UpdateGuildCountAsync()
        {
            if (_discordBotList is null || (DateTime.Now - _lastGuildCountUpdate).TotalMinutes < 30.0) return;

            int guilds = _client.ShardClients.Values.Select(x => x.Guilds.Count).Aggregate((a, b) => a + b);

            var recordedGuilds = (await _discordBotList.GetBotStatsAsync(_client.CurrentUser.Id)).GuildCount;
            if (recordedGuilds < guilds)
            {
                await _discordBotList.UpdateStats(guilds, _client.ShardClients.Count);
                _lastGuildCountUpdate = DateTime.Now;
                _log.Info($"Количество серверов обновлено до {guilds}");
            }
        }
    }
}
