using System.Security.Claims;
using System.Text.Json;
using Content.Server.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoffDiscordLink.Extensions;

namespace MoffDiscordLink.Pages.Discord;

[Authorize]
[ValidateAntiForgeryToken]
public class DiscordLinkModel(
    PostgresServerDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration
) : PageModel
{
    private const string TempKeyDiscordId = "PendingDiscordId";
    private const string TempKeyDiscordUsername = "PendingDiscordUsername";
    private const string TempKeyForUserId = "PendingDiscordForUserId";

    public string? DiscordUsername { get; set; }
    public string? DiscordId { get; set; }
    public bool IsLinked { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // Consume the Discord OAuth cookie immediately (single-use).
        // Discord ID is stored in TempData so OnPostLink can validate it server-side.
        if (await ConsumeDiscordCookieAsync() is var (discordId, discordUsername))
        {
            DiscordId = discordId;
            DiscordUsername = discordUsername;
            TempData[TempKeyDiscordId] = discordId;
            TempData[TempKeyDiscordUsername] = discordUsername;
            TempData[TempKeyForUserId] = User.Claims.GetUserId().ToString();
            return Page();
        }

        var userId = User.Claims.GetUserId();
        DiscordId = await dbContext.GetDiscordIdAsync(userId);
        if (DiscordId != null)
        {
            IsLinked = true;
            DiscordUsername = await LookupDiscordUsernameAsync(DiscordId);
        }

        return Page();
    }

    public IActionResult OnPostStartDiscordAuth() => Challenge(
        new AuthenticationProperties { RedirectUri = Url.Page("/Discord/Index") },
        AuthConsts.DiscordAuthScheme
    );

    public async Task<IActionResult> OnPostLink()
    {
        var pendingDiscordId = TempData[TempKeyDiscordId] as string;
        var pendingDiscordUsername = TempData[TempKeyDiscordUsername] as string;
        var pendingForUserId = TempData[TempKeyForUserId] as string;

        var userId = User.Claims.GetUserId();
        if (pendingDiscordId == null || pendingForUserId != userId.ToString())
        {
            TempData.SetStatusError("Discord authorization expired. Please try connecting again.");
            return RedirectToPage();
        }

        await dbContext.SetDiscordIdAsync(userId, pendingDiscordId);
        TempData.SetStatusInformation($"Discord account '{pendingDiscordUsername ?? pendingDiscordId}' linked successfully.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnlink()
    {
        var userId = User.Claims.GetUserId();
        await dbContext.SetDiscordIdAsync(userId, null);
        TempData.SetStatusInformation("Discord account unlinked.");
        return RedirectToPage();
    }

    private async Task<string?> LookupDiscordUsernameAsync(string discordId)
    {
        var botToken = configuration["Discord:BotToken"];
        if (string.IsNullOrEmpty(botToken))
            return null;

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", botToken);

        var response = await client.GetAsync($"https://discord.com/api/v10/users/{discordId}");
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (json.TryGetProperty("global_name", out var globalName) && globalName.GetString() is { } name)
            return name;
        return json.TryGetProperty("username", out var username) ? username.GetString() : null;
    }

    // Reads the Discord OAuth cookie, signs it out immediately (single-use), and returns the claims.
    private async Task<(string discordId, string discordUsername)?> ConsumeDiscordCookieAsync()
    {
        var discordAuth = await HttpContext.AuthenticateAsync(AuthConsts.DiscordCookie);
        if (!discordAuth.Succeeded)
            return null;

        await HttpContext.SignOutAsync(AuthConsts.DiscordCookie);

        if (discordAuth.Principal is not { } principal)
            throw new InvalidOperationException("Discord authentication succeeded with null principal.");

        if (principal.FindFirstValue(ClaimTypes.NameIdentifier) is not { } id)
            throw new InvalidOperationException("Discord principal is missing NameIdentifier claim.");

        var username = principal.FindFirstValue(ClaimTypes.Name) ?? id;
        return (id, username);
    }
}
