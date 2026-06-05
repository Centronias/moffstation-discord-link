using Content.Server.Database;
using Microsoft.EntityFrameworkCore;

namespace MoffDiscordLink.Extensions;

public static partial class ServerDbContextExt
{
    extension(ServerDbContext dbContext)
    {
        /// <summary>
        /// Returns the Discord ID for a single user, or null if not linked.
        /// </summary>
        public Task<string?> GetDiscordIdAsync(Guid userId)
        {
            return dbContext.Player
                .Where(p => p.UserId == userId)
                .Select(p => p.MoffPlayer != null ? p.MoffPlayer.DiscordId : null)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Sets or clears a user's Discord link. Pass null to remove the link.
        /// </summary>
        public async Task SetDiscordIdAsync(Guid userId, string? discordId)
        {
            var player = await dbContext.Player
                .Include(p => p.MoffPlayer)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (player == null)
                return;

            if (player.MoffPlayer != null)
            {
                player.MoffPlayer.DiscordId = discordId;
            }
            else if (discordId != null)
            {
                dbContext.Add(new MoffModel.MoffPlayer
                {
                    PlayerUserId = userId,
                    DiscordId = discordId,
                });
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
