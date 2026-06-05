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
        var identity = ctx.Principal?.Identities.FirstOrDefault(i => i.IsAuthenticated);
        if (identity == null)
            throw new InvalidOperationException("Unable to find authenticated identity after token validation.");

        var guid = identity.Claims.GetUserId();

        var whitelisted = await dbContext.Whitelist.AnyAsync(w => w.UserId == guid);
        if (!whitelisted)
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
            .FirstOrDefaultAsync(a => a.UserId == guid);

        if (adminData != null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, Constants.AdminRole));

            foreach (var flag in GetStringFlags(adminData))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, flag));
            }
        }
    }

    private static IEnumerable<string> GetStringFlags(DbAdmin admin)
    {
        var rankFlags = admin.AdminRank?.Flags.Select(f => f.Flag) ?? [];
        var flagsPos = admin.Flags.Where(f => !f.Negative).Select(f => f.Flag);
        var flagsNeg = admin.Flags.Where(f => f.Negative).Select(f => f.Flag);

        return rankFlags.Union(flagsPos).Except(flagsNeg);
    }
}
