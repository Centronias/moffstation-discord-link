using Content.Server.Database;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoffDiscordLink.Pagination;

namespace MoffDiscordLink.Pages.Discord;

public sealed record PlayerDiscordRow(Guid UserId, string LastSeenUserName, string? DiscordId);

public class DiscordManageModel(PostgresServerDbContext dbContext) : PageModel
{
    public PaginationState<PlayerDiscordRow> Pagination { get; } = new(100);
    public SortState<Player> SortState { get; } = new();
    public Dictionary<string, string?> AllRouteData { get; } = new();
    public string? CurrentFilter { get; set; }

    public async Task OnGetAsync(string? sort, string? search, int? pageIndex, int? perPage)
    {
        SortState.AddColumn("name", p => p.LastSeenUserName, SortOrder.Ascending);
        SortState.AddColumn("uid", p => p.UserId);
        SortState.Init(sort, AllRouteData);

        Pagination.Init(pageIndex, perPage, AllRouteData);

        CurrentFilter = search?.Trim();
        AllRouteData.Add("search", CurrentFilter);

        IQueryable<Player> query = dbContext.Player;

        if (!string.IsNullOrWhiteSpace(CurrentFilter))
        {
            if (Guid.TryParse(CurrentFilter, out var guid))
            {
                query = query.Where(p => p.UserId == guid);
            }
            else
            {
                query = query.Where(p =>
                    EF.Functions.ILike(p.LastSeenUserName, $"%{CurrentFilter}%") ||
                    (
                        p.MoffPlayer != null &&
                        p.MoffPlayer!.DiscordId != null &&
                        EF.Functions.ILike(p.MoffPlayer.DiscordId!, $"%{CurrentFilter}%")
                    ));
            }
        }

        var rowQuery = SortState.ApplyToQuery(query)
            .Select(p => new PlayerDiscordRow(p.UserId, p.LastSeenUserName, p.MoffPlayer != null ? p.MoffPlayer.DiscordId : null));

        await Pagination.LoadAsync(rowQuery);
    }
}
