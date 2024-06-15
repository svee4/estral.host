using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Estral.Host.Web.Features.Pages.Auth.Callback;

public class CallbackModel : PageModel
{
	public string? ErrorMessage { get; private set; }

	public async Task OnGet(
		[FromServices] IConfiguration config,
		[FromServices] HttpClient httpClient,
		[FromServices] UserManager<Database.User> userManager,
		[FromServices] SignInManager<Database.User> signInManager,
		[FromServices] Database.AppDbContext dbContext,
		[FromServices] ILogger<CallbackModel> logger)
	{

		var cookieState = Request.Cookies["estral.host.state"];
		var queryState = Request.Query["state"].ToString();

		// TODO: TODOOOO
		//if (string.IsNullOrWhiteSpace(cookieState) || cookieState != queryState)
		//{
		//	ErrorMessage = "Invalid state";
		//	return null;
		//}

		if (signInManager.IsSignedIn(User))
		{
			ErrorMessage = "Already signed in";
			return;
		}

		var code = Request.Query["code"].ToString();
		if (string.IsNullOrWhiteSpace(code))
		{
			ErrorMessage = "Missing required parameter code";
			return;
		}

		var clientId = config.GetRequiredValue("Auth:Discord:ClientId");
		var clientSecret = config.GetRequiredValue("Auth:Discord:ClientSecret");

		var appDomain = config.GetRequiredValue("AppDomain");

		TokenResponse? data;
		// get access token

		{

			using var request = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/oauth2/token");
			request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					{ "client_id", clientId },
					{ "client_secret", clientSecret },
					{ "grant_type", "authorization_code" },
					{ "code", code },
					{ "redirect_uri", $"https://{appDomain}/auth/callback" }
				});

			var result = await httpClient.SendAsync(request);

			var json = await result.Content.ReadAsStringAsync();
			if (!result.IsSuccessStatusCode || string.IsNullOrWhiteSpace(json))
			{
				ErrorMessage = $"Failed to get access token ({result.StatusCode})";
				logger.LogDebug("Failed to get access token: ({Status}) {Json}", result.StatusCode, json);
				return;
			}

			try
			{
				data = JsonSerializer.Deserialize<TokenResponse>(json);
			}
			catch (Exception ex)
			{
				ErrorMessage = $"Failed to parse access token";
				logger.LogError("Failed to parse access token json: {Exception}", ex);
				return;
			}

			if (data is null)
			{
				ErrorMessage = "Failed to parse access token";
				logger.LogError("Access token returned null!?");
				return;
			}
		}

		UserResponse? jsonUser;
		// get user info
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me");
			request.Headers.Authorization = new AuthenticationHeaderValue(data.token_type, data.access_token);
			var response = await httpClient.SendAsync(request);

			var json = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(json))
			{
				ErrorMessage = $"Failed to get user information ({response.StatusCode})";
				logger.LogError("Failed to get user information: ({Status}) {Json}", response.StatusCode, json);
				return;
			}

			try
			{
				jsonUser = JsonSerializer.Deserialize<UserResponse>(json);
			}
			catch (Exception ex)
			{
				ErrorMessage = $"Failed to parse user object";
				logger.LogError("Failed to parse user information json: {Exception}", ex);
				return;
			}

			if (jsonUser is null)
			{
				ErrorMessage = "Failed to parse user information";
				logger.LogError("User information json was null :/");
				return;
			}
		}

		if (!ulong.TryParse(jsonUser.id, out var discordUserId))
		{
			ErrorMessage = "Failed to parse user information";
			logger.LogError("User id could not be parsed into ulong ???");
			return;
		}

		var newUser = false;
		var user = await userManager.FindByLoginAsync(loginProvider: "Discord", providerKey: jsonUser.id);

		if (user is null)
		{
			newUser = true;
			// insert new user info

			user = Database.User.Create(discordUserId, jsonUser.username);

			if (await userManager.FindByNameAsync(user.UserName) is { })
			{
				user.UserName = new Guid().ToString(); // not doing this
			}


			if (await userManager.CreateAsync(user) is { Succeeded: false } createErr)
			{
				ErrorMessage = "Failed to create user";
				logger.LogError("Failed to create user for user {User}: {Errors}", new { Id = jsonUser.id, Username = jsonUser.username }, createErr.Errors.ToList());
				return;
			}

			var loginInfo = new UserLoginInfo("Discord", jsonUser.id, jsonUser.username);
			if (await userManager.AddLoginAsync(user, loginInfo) is { Succeeded: false } loginInfoErr)
			{
				ErrorMessage = "Failed to add login info";
				logger.LogError("Failed to add login info for user {User}: {Errors}", user, loginInfoErr.Errors.ToList());
				return;
			}
		}

		if (await signInManager.ExternalLoginSignInAsync("Discord", jsonUser.id, isPersistent: true) is { Succeeded: false } signInErr)
		{
			ErrorMessage = "Failed to sign in: " +
				(signInErr.IsLockedOut
				? "User locked out"
				: signInErr.IsNotAllowed
				? "Login not allowed"
				: "Unknown reason");
			return;
		}

		Response.Redirect(newUser ? "/setup" : "/");
	}


	record TokenResponse(string access_token, string token_type, int expires_in, string refresh_token, string scope);

	/*
	"{\"id\": \"158127066187825152\", \"username\": \"alexnj\", \"global_name\": \"alex\"
	*/
	record UserResponse(string id, string username, string? global_name, string? avatar, string discriminator, int public_flags, int flags, string? banner, string? banner_color, int? accent_color, string locale, bool mfa_enabled, int premium_type, string? avatar_decoration);

}
