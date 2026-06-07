using System.ComponentModel.DataAnnotations;
using Content.Server.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoffDiscordLink.Extensions;

namespace MoffDiscordLink.Pages.Discord;

public class DiscordEditModel(PostgresServerDbContext dbContext) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Player Player { get; set; } = default!;

    public class InputModel
    {
        [Display(Name = "Discord ID")]
        public string? DiscordId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid userId)
    {
        if (await LoadPlayerAsync(userId) is { } error)
            return error;

        Input.DiscordId = await dbContext.GetDiscordIdAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid userId)
    {
        if (await LoadPlayerAsync(userId) is { } error)
            return error;

        if (!ModelState.IsValid)
            return Page();

        await dbContext.SetDiscordIdAsync(userId, Input.DiscordId);
        TempData.SetStatusInformation($"Discord ID updated for '{Player.LastSeenUserName}'.");
        return RedirectToPage("./Manage");
    }

    public async Task<IActionResult> OnPostClearAsync(Guid userId)
    {
        if (await LoadPlayerAsync(userId) is { } error)
            return error;

        await dbContext.SetDiscordIdAsync(userId, null);
        TempData.SetStatusInformation($"Discord link cleared for '{Player.LastSeenUserName}'.");
        return RedirectToPage("./Manage");
    }

    // Sets Player and returns null on success, or NotFound() if the user doesn't exist.
    private async Task<IActionResult?> LoadPlayerAsync(Guid userId)
    {
        var player = await dbContext.Player.FirstOrDefaultAsync(p => p.UserId == userId);
        if (player == null)
            return NotFound();
        Player = player;
        return null;
    }
}
