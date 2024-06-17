using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Amazon.S3;
using Estral.Host.Web.Database;
using Estral.Host.Web.Infra;
using Estral.Host.Web.Infra.Extensions;
using Estral.Host.Web.Infra.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Features.Pages.Settings;

[Authorize]
[ValidateAntiForgeryToken]
public sealed class SettingsModel : PageModel
{
	public const int MaxPfpFileSizeBytes = 2 * 1000 * 1000; // 2mb
	public SettingsDataDto SettingsData { get; private set; }

	public string? ErrorMessage { get; private set; }

	public async Task OnGet(
		[FromServices] AppDbContext dbContext,
		CancellationToken token)
	{
		var userId = User.GetUserId() ?? throw new UnreachableException("Authorized user has no id");
		SettingsData = await GetSettingsData(dbContext, userId, token);
	}

	public async Task OnPost(
		[FromForm] PostDto postDto,
		[FromServices] AppDbContext dbContext,
		[FromServices] AuditLogService auditLogService,
		[FromServices] IAmazonS3 s3Client,
		[FromServices] IConfiguration config,
		[FromServices] ILogger<SettingsModel> logger,
		CancellationToken token)
	{
		var userId = User.GetUserId() ?? throw new UnreachableException("Authorized user has no id");
		SettingsData = await GetSettingsData(dbContext, userId, token);

		if (!ModelState.IsValid)
		{
			return;
		}

		var user = await dbContext.Users.FirstAsync(m => m.Id == userId, token);

		Dictionary<string, (string? Old, string? New)> changes = [];

		if (postDto.Username != user.UserName)
		{
			// check duplicate
			var isDuplicate = await dbContext.Users.AnyAsync(m => m.UserName == postDto.Username, token);
			if (isDuplicate)
			{
				ModelState.AddModelError(nameof(PostDto.Username), "Username is already taken");
				return;
			}
			changes.Add("Username", (user.UserName, postDto.Username));
			user.UserName = postDto.Username;
		}

		if (postDto.ProfileDescription != user.ProfileDescription)
		{
			changes.Add("ProfileDescription", (user.ProfileDescription, postDto.ProfileDescription));
			user.ProfileDescription = postDto.ProfileDescription;
		}

		if (postDto.ProfilePicture is not null)
		{
			changes.Add("ProfilePicture", ("a", "b"));
		}

		if (changes.Count is 0)
		{
			return;
		}

		await auditLogService.Add(new()
		{
			Category = AuditLogService.Categories.SettingsUpdate,
			UserId = user.Id,
			Username = user.UserName,
			RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
			Data = changes.ToDictionary(kvp => kvp.Key, kvp => (string?)$"{kvp.Value.Old} => {kvp.Value.New}")
		}, CancellationToken.None);

		await dbContext.SaveChangesAsync(token);
		SettingsData = await GetSettingsData(dbContext, userId, token);


		if (postDto.ProfilePicture is { } pfp)
		{
			// dont need to delete previous file, it will be overwritten

			var ms = new MemoryStream();
			await pfp.CopyToAsync(ms, token);

			var result = await AmazonS3Extensions.UploadObject(
				s3Client: s3Client,
				bucketName: config.GetRequiredValue("S3:BucketName"),
				key: $"pfp/{userId}",
				contentType: pfp.ContentType,
				inputStream: ms,
				token: token
			);

			if ((int)result.HttpStatusCode >= 400)
			{
				ErrorMessage = "Failed to upload new profile picture";
				logger.LogError("Failed to upload profile picture ({Id}) to cdn: {StatusCode} {Metadata}",
					userId,
					result.HttpStatusCode,
					result.ResponseMetadata);
				return;
			}
		}


	}

	[NonHandler]
	private static async Task<SettingsDataDto> GetSettingsData(AppDbContext dbContext, int userId, CancellationToken token)
	{
		var data = await dbContext.Users.FirstAsync(m => m.Id == userId, token);
		return new SettingsDataDto
		{
			Username = data.UserName,
			ProfileDescription = data.ProfileDescription
		};
	}

	public sealed class SettingsDataDto
	{
		public string Username { get; init; }
		public string? ProfileDescription { get; init; }
	}

	public sealed class PostDto
	{
		[Required]
		[MaxLength(Database.User.UsernameMaxLength)]
		public string Username { get; init; }

		[MaxLength(Database.User.ProfileDescriptionMaxLength)]
		public string? ProfileDescription { get; init; }

		[FileValidation(MaxInclusiveSizeInBytes = MaxPfpFileSizeBytes,
			AllowedFileTypesPreset = FileValidationAttribute.FileTypes.Preset.ImageAndGif)]
		public IFormFile? ProfilePicture { get; init; }
	}
}
