using System.Security.Claims;
using Content.Server.Database;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using MoffDiscordLink.Extensions;
using DbAdmin = Content.Server.Database.Admin;

namespace MoffDiscordLink;

public sealed class LoginHandler(PostgresServerDbContext dbContext, LinkGenerator linkGenerator)
{
    public async Task HandleTokenValidated(TokenValidatedContext ctx)
    {
        var identity = ctx.Principal?.Identities.FirstOrDefault(i => i.IsAuthenticated)
            ?? throw new InvalidOperationException("Unable to find authenticated identity after token validation.");

        var userId = identity.Claims.GetUserId();

        if (!await dbContext.Whitelist.AnyAsync(w => w.UserId == userId))
        {
            ctx.Response.Redirect(linkGenerator.GetPathByPage(ctx.HttpContext, "/LoginFailed")!);
            ctx.HandleResponse();
            return;
        }

        var adminData = await dbContext.Admin
            .Include(a => a.AdminRank)
            .ThenInclude(r => r!.Flags)
            .Include(a => a.Flags)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (adminData != null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, Constants.AdminRole));
            foreach (var flag in GetAdminRoleFlags(adminData))
                identity.AddClaim(new Claim(ClaimTypes.Role, flag));
        }
    }

    private static IEnumerable<string> GetAdminRoleFlags(DbAdmin admin)
    {
        var rankFlags = admin.AdminRank?.Flags.Select(f => f.Flag) ?? [];
        var positiveFlags = admin.Flags.Where(f => !f.Negative).Select(f => f.Flag);
        var negativeFlags = admin.Flags.Where(f => f.Negative).Select(f => f.Flag);
        return rankFlags.Union(positiveFlags).Except(negativeFlags);
    }
}
