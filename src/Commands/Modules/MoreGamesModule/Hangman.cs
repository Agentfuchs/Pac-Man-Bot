﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot.Commands.Modules
{
    partial class MoreGamesModule
    {
        [Command("hangman"), Alias("hang")]
        [Remarks("Start a game of Hangman in a channel")]
        [Summary("When this command is used, the channel becomes a public game of hangman.\n" +
                 "Anyone will be able to guess either a letter or the full word. Up to 10 wrong guesses!\n\n" +
                 "You can use **{prefix}hangman choose** if you want to choose a word or phrase for your friends to guess. " +
                 "Don't send it in the chat! The bot will ask in private.")]
        public async Task StartHangman([Remainder]string args = null)
        {
            var existingGame = Games.GetForChannel(Context.Channel.Id);
            if (existingGame != null)
            {
                await ReplyAsync(existingGame.UserId.Contains(Context.User.Id)
                    ? $"You're already playing a game in this channel!\nUse `{Context.Prefix}cancel` if you want to cancel it."
                    : $"There is already a different game in this channel!\nWait until it's finished or try doing `{Context.Prefix}cancel`");
                return;
            }

            if (args != null)
            {
                await ReplyAsync(
                    $"You can use `{Context.Prefix}hangman choose` if you want to choose what the rest will have to guess.\n" +
                    $"The bot will ask you in private!");
                return;
            }

            var game = new HangmanGame(Context.Channel.Id, Services);
            var message = await ReplyAsync(game.GetEmbed());
            game.MessageId = message.Id;
            Games.Add(game);
        }


        [Command("hangman choose"), Alias("hang choose", "hangman word", "hang word"), Priority(1), HideHelp]
        [Summary("When this command is used, you will be sent a DM asking for a word or phrase in private. " +
                 "Once you give it, the game will start in the original channel where you used this command.\n" +
                 "Anyone will be able to guess either a letter or the full phrase. Up to 10 wrong guesses!\n\n" +
                 "To start a normal game with a random word, use **{prefix}hangman**")]
        public async Task StartHangmanCustom()
        {
            if (Context.Guild == null)
            {
                await ReplyAsync($"There's nobody here to guess! To play alone, use `hangman`");
                return;
            }

            var existingGame = Games.GetForChannel(Context.Channel.Id);
            if (existingGame != null)
            {
                await ReplyAsync(existingGame.UserId.Contains(Context.User.Id)
                    ? $"You're already playing a game in this channel!\nUse `{Context.Prefix}cancel` if you want to cancel it."
                    : $"There is already a different game in this channel!\nWait until it's finished or try doing `{Context.Prefix}cancel`");
                return;
            }


            var game = new HangmanGame(Context.Channel.Id, Context.User.Id, Services);
            Games.Add(game);

            var userChannel = await Context.User.GetOrCreateDMChannelAsync();
            try
            {
                await userChannel.SendMessageAsync(
                    $"Send the secret word or phrase for the {game.GameName} game in {Context.Channel.Mention()}:");
            }
            catch (HttpException e) when (e.DiscordCode == 50007) // Can't send DMs
            {
                await ReplyAsync($"{Context.User.Mention} You must enable DMs!");
                Games.Remove(game);
                return;
            }

            var message = await ReplyAsync($"{Context.User.Mention} check your DMs!", game.GetEmbed());
            game.MessageId = message.Id;

            while (true)
            {
                var response = await Input.GetResponse(x =>
                    x.Channel.Id == userChannel.Id && x.Author.Id == Context.User.Id, 90);

                if (response == null)
                {
                    Games.Remove(game);
                    game.State = State.Cancelled;
                    await userChannel.SendMessageAsync("Timed out 💨");
                    message = await game.GetMessage();
                    if (message != null) await message.ModifyAsync(game.GetMessageUpdate());
                }

                string word = response.Content.ToUpperInvariant().Replace('\n', ' ');
                string wf = response.Content.Contains(' ') ? "phrase" : "word";

                if (!HangmanGame.Alphabet.IsMatch(word))
                {
                    await userChannel.SendMessageAsync(
                        $"Sorry, but your secret {wf} can only contain alphabet characters (A-Z).\nTry again.");
                }
                else if (word.Length > 40)
                {
                    await userChannel.SendMessageAsync(
                        $"Sorry, but your secret {wf} can only be up to 40 characters long.\nTry again.");
                }
                else if (word.Count(x => x == ' ') > 5)
                {
                    await userChannel.SendMessageAsync(
                        $"Sorry, but your secret phrase can only be up to six words long.\nTry again.");
                }
                else
                {
                    game.SetWord(word);
                    await response.AutoReactAsync();

                    message = await game.GetMessage();
                    if (message == null) await ReplyAsync(game.GetEmbed());
                    else await message.ModifyAsync(game.GetMessageUpdate());

                    return;
                }
            }
        }
    }
}