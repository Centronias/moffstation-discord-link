using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace MoffDiscordLink.Controllers;

[Controller]
[Route("/Login")]
public class Login : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return Challenge(new AuthenticationProperties { RedirectUri = Url.Page("/Index") });
    }

    [HttpPost]
    [Route("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthConsts.Ss14Cookie);
        await HttpContext.SignOutAsync(AuthConsts.DiscordCookie);
        return RedirectToPage("/Index");
    }
}
