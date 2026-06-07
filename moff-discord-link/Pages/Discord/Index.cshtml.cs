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
    public string? DiscordId { get; private set; }
    public string? DiscordUsername { get; private set; }
    public bool IsLinked { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // OAuth callback: consume the single-use Discord cookie and link immediately.
        if (await TryConsumeDiscordOAuthAsync() is { } discord)
        {
            await dbContext.SetDiscordIdAsync(User.Claims.GetUserId(), discord.Id);
            TempData.SetStatusInformation($"Discord account '{discord.Username}' linked successfully.");
            return RedirectToPage();
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

    public async Task<IActionResult> OnPostUnlink()
    {
        await dbContext.SetDiscordIdAsync(User.Claims.GetUserId(), null);
        TempData.SetStatusInformation("Discord account unlinked.");
        return RedirectToPage();
    }

    // Returns the Discord identity from the OAuth cookie and immediately invalidates it.
    private async Task<(string Id, string Username)?> TryConsumeDiscordOAuthAsync()
    {
        var result = await HttpContext.AuthenticateAsync(AuthConsts.DiscordCookie);
        if (!result.Succeeded)
            return null;

        await HttpContext.SignOutAsync(AuthConsts.DiscordCookie);

        var principal = result.Principal
            ?? throw new InvalidOperationException("Discord authentication succeeded with null principal.");

        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Discord principal is missing NameIdentifier claim.");

        var username = principal.FindFirstValue(ClaimTypes.Name) ?? id;
        return (id, username);
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
}
