using Discord;
using System.Threading.Tasks;

namespace PacManBot.Games
{
    /// <summary>
    /// The interface for <see cref="IChannelGame"/>s that receive reaction input.
    /// </summary>
    public interface IReactionsGame : IChannelGame
    {
        /// <summary>Whether the given value is a valid input given the player sending it.</summary>
        bool IsInput(IEmote value, ulong userId);

        /// <summary>Executes an input expected to be valid.</summary>
        Task InputAsync(IEmote input, ulong userId = 1);
    }
}
