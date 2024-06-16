using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;

namespace Estral.Host.Web.Features.Pages.Auth.Callback;

[ValidateAntiForgeryToken]
public class CallbackModel : PageModel
{
	public string? ErrorMessage { get; private set; }
	public bool PostFast { get; private set; }

	public OAuthParamsDto OAuthParams {  get; private set; }

	private static JsonSerializerOptions WebJsonSerializerOptions { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	public void OnGet(string code, string state)
	{
		PostFast = true;
		OAuthParams = new OAuthParamsDto
		{
			Code = code,
			State = state,
		};
	}

	public async Task OnPost(
		[FromForm] PostDto postDto,
		[FromServices] IConfiguration config,
		[FromServices] HttpClient httpClient,
		[FromServices] UserManager<Database.User> userManager,
		[FromServices] SignInManager<Database.User> signInManager,
		[FromServices] Database.AppDbContext dbContext,
		[FromServices] IAmazonS3 s3client,
		[FromServices] ILogger<CallbackModel> logger,
		CancellationToken token)
	{
		OAuthParams = new OAuthParamsDto
		{
			Code = "",
			State = "",
		};

		var cookieState = Request.Cookies["estral.host.state"];

		if (string.IsNullOrWhiteSpace(cookieState) || cookieState != postDto.State)
		{
			ErrorMessage = "Invalid state";
			return;
		}

		if (signInManager.IsSignedIn(User))
		{
			ErrorMessage = "Already signed in";
			return;
		}

		if (string.IsNullOrWhiteSpace(postDto.Code))
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
					{ "code", postDto.Code },
					{ "redirect_uri", $"https://{appDomain}/auth/callback" }
				});

			var result = await httpClient.SendAsync(request, token);

			var json = await result.Content.ReadAsStringAsync(token);
			if (!result.IsSuccessStatusCode || string.IsNullOrWhiteSpace(json))
			{
				ErrorMessage = $"Failed to get access token ({result.StatusCode})";
				logger.LogDebug("Failed to get access token: ({Status}) {Json}", result.StatusCode, json);
				return;
			}

			try
			{
				data = JsonSerializer.Deserialize<TokenResponse>(json, WebJsonSerializerOptions);
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
			request.Headers.Authorization = new AuthenticationHeaderValue(data.TokenType, data.AccessToken);
			var response = await httpClient.SendAsync(request, token);

			var json = await response.Content.ReadAsStringAsync(token);
			if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(json))
			{
				ErrorMessage = $"Failed to get user information ({response.StatusCode})";
				logger.LogError("Failed to get user information: ({Status}) {Json}", response.StatusCode, json);
				return;
			}

			try
			{
				jsonUser = JsonSerializer.Deserialize<UserResponse>(json, WebJsonSerializerOptions);
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

		if (!ulong.TryParse(jsonUser.Id, out var discordUserId))
		{
			ErrorMessage = "Failed to parse user information";
			logger.LogError("User id could not be parsed into ulong ???");
			return;
		}

		var newUser = false;
		var user = await userManager.FindByLoginAsync(loginProvider: "Discord", providerKey: jsonUser.Id);

		if (user is null)
		{
			newUser = true;
			// insert new user info

			user = Database.User.Create(discordUserId, jsonUser.Username);

			if (await userManager.FindByNameAsync(user.UserName) is { })
			{
				user.UserName = new Guid().ToString(); // not doing this
			}


			if (await userManager.CreateAsync(user) is { Succeeded: false } createErr)
			{
				ErrorMessage = "Failed to create user";
				logger.LogError("Failed to create user for user {User}: {Errors}", new { Id = jsonUser.Id, Username = jsonUser.Username }, createErr.Errors.ToList());
				return;
			}

			var loginInfo = new UserLoginInfo("Discord", jsonUser.Id, jsonUser.Username);
			if (await userManager.AddLoginAsync(user, loginInfo) is { Succeeded: false } loginInfoErr)
			{
				ErrorMessage = "Failed to add login info";
				logger.LogError("Failed to add login info for user {User}: {Errors}", user, loginInfoErr.Errors.ToList());
				return;
			}

			// generate profile picture for user
			var thing = await httpClient.GetStreamAsync($"https://api.dicebear.com/9.x/identicon/jpg?seed={user.Id}", token);
			var request = new PutObjectRequest()
			{
				BucketName = config.GetRequiredValue("S3:BucketName"),
				Key = $"pfp/{user.Id}",
				ContentType = "image/jpg",
				InputStream = thing,
				DisablePayloadSigning = true
			};

			var result = await s3client.PutObjectAsync(request, token);
			if ((int)result.HttpStatusCode >= 400)
			{
				logger.LogError("Failed to get default avatar for {UserId}, request failed with status code{StatusCode} and metadata {Metadata}",
					user.Id, result.HttpStatusCode, result.ResponseMetadata);
			}

		}

		if (await signInManager.ExternalLoginSignInAsync("Discord", jsonUser.Id, isPersistent: true) is { Succeeded: false } signInErr)
		{
			ErrorMessage = "Failed to sign in: " +
				(signInErr.IsLockedOut
				? "User locked out"
				: signInErr.IsNotAllowed
				? "Login not allowed"
				: "Unknown reason");
			return;
		}

		Response.Redirect(newUser ? "/settings" : "/");
	}


	private class TokenResponse
	{
		[JsonPropertyName("access_token")]
		public required string AccessToken { get; init; }

		[JsonPropertyName("token_type")]
		public required string TokenType { get; init; }

		[JsonPropertyName("expires_in")]
		public required int ExpiresIn { get; init; }

		[JsonPropertyName("refresh_token")]
		public required string RefreshToken { get; init; } 
		public required string Scope { get; init; }
	}


	public sealed class UserResponse
	{
		public required string Id { get; init; }
		public required string Username { get; init; }
		public required string? Avatar { get; init; }
	}


	public sealed class OAuthParamsDto
	{
		public string Code { get; init; }

		public string State { get; init; }
	}

	public sealed class PostDto
	{
		[Required]
		public string Code { get; set; }
		[Required]
		public string State { get; set; }
	}
}
