﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;

namespace PacManBot.Services
{
    /// <summary>
    /// Manages active game instances from this bot as well as their files on disk.
    /// </summary>
    public class GameService
    {
        private static readonly IEnumerable<(string key, Type type)> StoreableGameTypes = ReflectionExtensions.AllTypes
            .SubclassesOf<IStoreableGame>()
            .Select(t => (t.CreateInstance<IStoreableGame>().FilenameKey, t))
            .OrderByDescending(t => t.FilenameKey.Length)
            .ToArray();

        private static readonly JsonSerializerSettings GameJsonSettings = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        private readonly IServiceProvider _services;
        private readonly LoggingService _log;
        private readonly ConcurrentDictionary<ulong, IChannelGame> _games;
        private readonly ConcurrentDictionary<(ulong, Type), IUserGame> _userGames;


        /// <summary>Enumerates through all active channel-specific games concurrently.</summary>
        public IEnumerable<IChannelGame> AllChannelGames => _games.Select(x => x.Value);
        /// <summary>Enumerates through all active user-specific games concurrently.</summary>
        public IEnumerable<IUserGame> AllUserGames => _userGames.Select(x => x.Value);
        /// <summary>Enumerates through all active games of any type.</summary>
        public IEnumerable<IBaseGame> AllGames => AllChannelGames.Cast<IBaseGame>().Concat(AllUserGames.Cast<IBaseGame>());


        public GameService(IServiceProvider services, LoggingService log)
        {
            _services = services;
            _log = log;

            _games = new ConcurrentDictionary<ulong, IChannelGame>();
            _userGames = new ConcurrentDictionary<(ulong, Type), IUserGame>();
        }




        /// <summary>Retrieves the active game for the specified channel. Null if not found.</summary>
        public IChannelGame GetForChannel(ulong channelId)
        {
            return _games.TryGetValue(channelId, out var game) ? game : null;
        }


        /// <summary>Retrieves the active game for the specified channel, cast to the desired type.
        /// Null if not found or if it is the wrong type.</summary>
        public TGame GetForChannel<TGame>(ulong channelId) where TGame : class, IChannelGame
        {
            return GetForChannel(channelId) as TGame;
        }


        /// <summary>Retrieves the specified user's game of the desired type. Null if not found.</summary>
        public TGame GetForUser<TGame>(ulong userId) where TGame : class, IUserGame
        {
            return _userGames.TryGetValue((userId, typeof(TGame)), out var game) ? (TGame)game : null;
        }


        /// <summary>Retrieves the specified user's game of the desired type. Null if not found.</summary>
        public IUserGame GetForUser(ulong userId, Type type)
        {
            return _userGames.TryGetValue((userId, type), out var game) ? game : null;
        }


        /// <summary>Adds a new game to the collection of channel games or user games.</summary>
        public void Add(IBaseGame game)
        {
            if (game is IUserGame uGame) _userGames.TryAdd((uGame.OwnerId, uGame.GetType()), uGame);
            else if (game is IChannelGame cGame) _games.TryAdd(cGame.ChannelId, cGame);
        }


        /// <summary>Permanently deletes a game from the collection of channel games or user games, 
        /// as well as its savefile if there is one.</summary>
        public void Remove(IBaseGame game, bool doLog = true)
        {
            if (game is null) return;

            bool success = false;

            if (game is IUserGame uGame)
            {
                success = _userGames.TryRemove((uGame.OwnerId, uGame.GetType()));
            }
            else if (game is IChannelGame cGame)
            {
                success = _games.TryRemove(cGame.ChannelId);
            }

            if (game is IStoreableGame sGame && File.Exists(sGame.GameFile()))
            {
                try { File.Delete(sGame.GameFile()); }
                catch (IOException e) { _log.Exception($"Не удалось удалить {sGame.GameFile()}", e); }
            }

            if (success && doLog)
            {
                _log.Debug($"Удалено {game.GetType().Name} в {game.IdentifierId()}");
            }
        }



        /// <summary>Stores a backup of the game on disk asynchronously, to be loaded the next time the bot starts.</summary>
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "future-proofing")]
        public async Task SaveAsync(IStoreableGame game)
        {
            game.LastPlayed = DateTime.Now;
            await File.WriteAllTextAsync(game.GameFile(), JsonConvert.SerializeObject(game, GameJsonSettings), Encoding.UTF8);
        }




        /// <summary>Reload the entire game collection from disk.</summary>
        public async Task LoadGamesAsync()
        {
            _games.Clear();
            _userGames.Clear();

            if (!Directory.Exists(Files.GameFolder))
            {
                Directory.CreateDirectory(Files.GameFolder);
                _log.Warning($"Создана отсутствующая директория \"{Files.GameFolder}\"");
                return;
            }

            uint fail = 0;
            bool firstFail = true;

            foreach (string file in Directory.GetFiles(Files.GameFolder).Where(f => f.EndsWith(Files.GameExtension)))
            {
                try
                {
                    Type gameType = StoreableGameTypes.First(x => file.Contains(x.key)).type;
                    string json = await File.ReadAllTextAsync(file);
                    var game = (IStoreableGame)JsonConvert.DeserializeObject(json, gameType, GameJsonSettings);
                    game.PostDeserialize(_services);

                    if (game is IUserGame uGame) _userGames.TryAdd((uGame.OwnerId, uGame.GetType()), uGame);
                    else if (game is IChannelGame cGame) _games.TryAdd(cGame.ChannelId, cGame);
                }
                catch (Exception e)
                {
                    _log.Log(
                        $"Не удалось загрузить игру в {file}: {(firstFail ? e.ToString() : e.Message)}",
                        firstFail ? LogLevel.Error : LogLevel.Trace);
                    fail++;
                    firstFail = false;
                }
            }

            _log.Info($"Загружено {_games.Count + _userGames.Count} игр{$" с {fail} ошибками".If(fail > 0)}");
        }
    }
}
