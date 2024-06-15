using System.Collections.Concurrent;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Estral.Host.Web.Features.Pages.Auth.Login;

public class LoginModel : PageModel
{

	public async Task<RedirectResult> OnGet(
		[FromServices] SignInManager<Database.User> signinManager,
		[FromServices] IConfiguration config)
	{
		if (signinManager.IsSignedIn(User))
		{
			await signinManager.SignOutAsync();
		}

		var clientId = config.GetRequiredValue("Auth:Discord:ClientId");
		var redirectUri = "https://" + config.GetRequiredValue("AppDomain") + "/auth/callback";

		var scope = "identify";
		var state = Guid.NewGuid().ToString();

		var url = $"https://discord.com/api/oauth2/authorize?response_type=code&prompt=none&client_id={clientId}&redirect_uri={redirectUri}&scope={scope}&state={state}";

		Response.Cookies.Append("estral.host.state", state, new CookieOptions()
		{
			Domain = config.GetRequiredValue("AppDomain"),
			HttpOnly = true,
			Path = "/Auth",
			Secure = true,
			Expires = DateTimeOffset.Now.AddMinutes(5),
			SameSite = SameSiteMode.Strict
		});

		return Redirect(url);
	}
}
