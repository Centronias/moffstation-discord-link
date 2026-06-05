using System.Security.Claims;

namespace MoffDiscordLink.Extensions;

public record struct Ss14IdentityData(string Name);

public static class ClaimExt
{
    extension(ClaimsPrincipal principal)
    {
        public Ss14IdentityData? Ss14Identity()
        {
            return principal.Identities.Any(i => i.IsAuthenticated)
                ? new Ss14IdentityData(principal.Claims.Single(c => c.Type == "name").Value)
                : null;
        }
    }

    extension(IEnumerable<Claim> claims)
    {
        public Guid GetUserId() => new(claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
    }
}
