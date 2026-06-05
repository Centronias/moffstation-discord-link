using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MoffDiscordLink.Pages;

public class AccessDenied : PageModel
{
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl)
    {
        ReturnUrl = returnUrl;
    }
}
