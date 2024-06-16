using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Estral.Host.Web.Features.Pages.Auth.Logout;

public class LogoutModel : PageModel
{
	public async Task<RedirectResult> OnGet(
		[FromServices] SignInManager<Database.User> signInManager)
	{
		if (signInManager.IsSignedIn(User))
		{
			await signInManager.SignOutAsync();
		}

		return Redirect("/");
	}
}
