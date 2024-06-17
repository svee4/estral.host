using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Amazon.S3;
using Amazon.S3.Model;
using Estral.Host.Web.Infra;
using Estral.Host.Web.Infra.Authorization;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Features.Pages.Content.Content;

[ValidateAntiForgeryToken]
public class ContentModel : PageModel
{

	[MemberNotNullWhen(false, nameof(ContentData))]
	public new bool NotFound { get; private set; }

	public string? ErrorMessage { get; private set; }
	public ContentDto? ContentData { get; private set; }

	public bool IsOwner { get; private set; }

	public async Task OnGet(
		int id,
		[FromServices] Database.AppDbContext dbContext,
		[FromServices] UserManager<Database.User> userManager,
		CancellationToken token)
	{
		var content = await dbContext.Contents
			.Include(m => m.Owner)
			.Where(m => m.Id == id)
			.FirstOrDefaultAsync(token);

		if (content is null)
		{
			NotFound = true;
			return;
		}

		var userId = (await userManager.GetUserAsync(User))?.Id;
		IsOwner = userId == content.Owner.Id;

		ContentData = new ContentDto()
		{
			Id = content.Id,
			Title = content.Title,
			Description = content.Description,
			OwnerId = content.Owner.Id,
			OwnerName = content.Owner.UserName
		};

		var og = new OpenGraphModel()
		{
			Id = content.Id,
			Title = content.Title
		};

		ViewData[ViewDataKeys.OpenGraphModel] = og;
	}

	// the post is a delete
	public async Task<IActionResult> OnPost(
		[FromRoute] int id,
		[FromServices] UserManager<Database.User> userManager,
		[FromServices] Database.AppDbContext dbContext,
		[FromServices] IAmazonS3 s3Client,
		[FromServices] AuditLogService auditLogService,
		[FromServices] IConfiguration config,
		[FromServices] ILogger<ContentModel> logger,
		CancellationToken token)
	{
		var user = await userManager.GetUserAsync(User);
		if (user is null)
		{
			ErrorMessage = "Not logged in";

			await auditLogService.Add(new()
			{
				Category = AuditLogService.Categories.Authorization,
				Message = "Unauthenticated attempt to delete content",
				RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
				Data = new()
				{
					["contentId"] = id.ToString(CultureInfo.InvariantCulture),
				},
			}, CancellationToken.None);

			return Unauthorized();
		}

		var content = await dbContext.Contents.Where(m => m.Id == id).FirstOrDefaultAsync(token);
		if (content is null)
		{
			return BadRequest();
		}

		if (user.Id != content.OwnerId && !User.IsInRole(Roles.Admin))
		{
			ErrorMessage = "Not authorized";

			await auditLogService.Add(new()
			{
				Category = AuditLogService.Categories.Authorization,
				Message = "Unauthorized attempt to delete content",
				RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
				Username = user.UserName,
				UserId = user.Id,
				Data = new()
				{
					["contentId"] = id.ToString(CultureInfo.InvariantCulture)
				},
			}, CancellationToken.None);

			return Unauthorized();
		}

		await auditLogService.Add(new()
		{
			Category = AuditLogService.Categories.ContentDelete,
			Message = "Content deleted",
			Username = user.UserName,
			UserId = user.Id,
			Data = new()
			{
				["contentId"] = content.Id.ToString(CultureInfo.InvariantCulture),
				["contentTitle"] = content.Title
			}
		}, CancellationToken.None);

		dbContext.Contents.Remove(content);
		await dbContext.SaveChangesAsync(token);

		var request = new DeleteObjectRequest
		{
			BucketName = config.GetRequiredValue("S3:BucketName"),
			Key = id.ToString(CultureInfo.InvariantCulture)
		};

		var result = await AmazonS3Extensions.DeleteObject(
			s3Client,
			config.GetRequiredValue("S3:BucketName"),
			id.ToString(CultureInfo.InvariantCulture),
			CancellationToken.None);

		if ((int)result.HttpStatusCode >= 400)
		{
			ErrorMessage = "Failed to remove content from cdn";
			logger.LogError("Failed to remove content ({Id}) from cdn: {StatusCode} {Metadata}", 
				id, 
				result.HttpStatusCode,
				result.ResponseMetadata);
			return Page();
		}

		NotFound = true;
		return Page();
	}

	public sealed class ContentDto
	{
		public required int Id { get; set; }
		public required string Title { get; set; }
		public required string? Description { get; set; }
		public required int OwnerId { get; set; }
		public required string OwnerName { get; set; }
	}
}
