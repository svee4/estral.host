using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Estral.Host.Web.Infra;
using System.Diagnostics;
using Estral.Host.Web.Infra.Validation;

namespace Estral.Host.Web.Features.Pages.Content.Upload;

[Authorize]
[ValidateAntiForgeryToken]
public class UploadModel : PageModel
{

	public const int TitleMaxLength = Database.Content.TitleMaxLength;
	public const int DescriptionMaxLength = Database.Content.DescriptionMaxLength;
	public const int FileMaxSizeBytes = 10 * 1000 * 1000;

	public string? ErrorMessage { get; private set; }

	public void OnGet() { }

	public async Task OnPost(
		[FromForm] PostDto postDto,
		[FromServices] Database.AppDbContext dbContext,
		[FromServices] UserManager<Database.User> userManager,
		[FromServices] R2Client r2Client,
		[FromServices] AuditLogService auditLogService,
		[FromServices] IConfiguration config,
		[FromServices] ILogger<UploadModel> logger,
		CancellationToken token)
	{
		if (!ModelState.IsValid)
		{
			Debugger.Break();
			return;
		}

		var title = StringExtensions.NormalizeToNull(postDto.Title.Trim());
		var description = StringExtensions.NormalizeToNull(postDto.Description?.Trim());

		if (title is null)
		{
			ModelState.AddModelError(nameof(PostDto.Title), "Title cannot be empty or only whitespace");
			return;
		}

		var user = await userManager.GetUserAsync(User) ?? throw new Exception("Null authorized user??");

		if (await dbContext.Contents.AnyAsync(m => m.Owner.Id == user.Id && m.Title == postDto.Title, token))
		{
			ModelState.AddModelError(nameof(PostDto.Title), "Duplicate title");
			return;
		}

		await auditLogService.Add(new()
		{
			Category = AuditLogService.Categories.ContentUpload,
			UserId = user.Id,
			Username = user.UserName,
			RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
			Data = new()
			{
				{ "title", title },
				{ "description", description }
			}
		}, CancellationToken.None);

		var content = Database.Content.Create(title, description, user);

		if (postDto.Tags is { Count: > 0 })
		{
			var existingTags = await dbContext.Tags.Where(m => postDto.Tags.Contains(m.Name)).ToListAsync(token);

			var anyNew = false;
			foreach (var tag in postDto.Tags)
			{
				if (!existingTags.Any(m => m.Name == tag))
				{
					anyNew = true;
					var newTag = Database.Tag.Create(tag);
					dbContext.Tags.Add(newTag);
					existingTags.Add(newTag);
				}
			}

			if (anyNew)
			{
				await dbContext.SaveChangesAsync(token);
			}

			foreach (var tag in existingTags)
			{
				content.Tags.Add(tag);
			}
		}

		await dbContext.Contents.AddAsync(content, token);
		await dbContext.SaveChangesAsync(token);

		var ms = new MemoryStream();
		await postDto.File.CopyToAsync(ms, token);

		var result = await r2Client.UploadObject(
			key: content.Id.ToString(CultureInfo.InvariantCulture),
			contentType: postDto.File.ContentType,
			inputStream: ms,
			CancellationToken.None
		);

		if ((int)result.HttpStatusCode >= 400)
		{
			dbContext.Contents.Remove(content);
			await dbContext.SaveChangesAsync(token);
			ErrorMessage = "Upload failed";
			logger.LogError("S3 upload failed with status code {StatusCode} and metadata {Metadata}",
				result.HttpStatusCode,
				result.ResponseMetadata);
			return;
		}

		Response.Redirect($"/content/{content.Id}");
	}

	public sealed class PostDto : IValidatableObject
	{

		[Required]
		[FileValidation(MaxInclusiveSizeInBytes = FileMaxSizeBytes, AllowedFileTypesPreset = FileValidationAttribute.FileTypes.Preset.ImageAndGif)]
		public IFormFile File { get; set; } = null!;

		[Required, MaxLength(TitleMaxLength)]
		public string Title { get; set; } = null!;

		[MaxLength(DescriptionMaxLength)]
		public string? Description { get; set; }

		[MaxLength(10)]
		public IReadOnlyList<string>? Tags { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (Tags is { Count: > 0 })
			{
				foreach (var tag in Tags)
				{
					if (tag.Length > Database.Tag.NameMaxLength)
					{
						yield return new ValidationResult($"Tag name maximum length is {Database.Tag.NameMaxLength}", [nameof(Tags)]);
						break;
					}
				}
			}
		}
	}
}
