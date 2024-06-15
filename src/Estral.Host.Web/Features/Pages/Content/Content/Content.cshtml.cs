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
	}

	public async Task OnPost(
		[FromRoute] int id,
		[FromServices] UserManager<Database.User> userManager,
		[FromServices] Database.AppDbContext dbContext,
		[FromServices] AmazonS3Client s3Client,
		[FromServices] AuditLogService auditLogService,
		[FromServices] IConfiguration config,
		[FromServices] ILogger<ContentModel> logger,
		CancellationToken token)
	{
		var user = await userManager.GetUserAsync(User);
		if (user is null)
		{
			ErrorMessage = "Not logged in";
			await auditLogService.Add(
				category: AuditLogService.Categories.Authorization,
				message: "Unauthenticated attempt to delete content",
				requestIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
				data: new Dictionary<string, string>()
				{
					["contentId"] = id.ToString(CultureInfo.InvariantCulture),
				},
				token: CancellationToken.None
			);
			return;
		}

		var content = await dbContext.Contents.Where(m => m.Id == id).FirstOrDefaultAsync(token);
		if (content is null)
		{
			return;
		}

		if (user.Id != content.OwnerId && !User.IsInRole(Roles.Admin))
		{
			ErrorMessage = "Not authorized";
			await auditLogService.Add(
				category: AuditLogService.Categories.Authorization,
				message: "Unauthorized attempt to delete content",
				requestIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
				username: user.UserName,
				userId: user.Id,
				data: new Dictionary<string, string>()
				{
					["contentId"] = id.ToString(CultureInfo.InvariantCulture)
				},
				token: CancellationToken.None
			);
			return;
		}

		// yes i log deletions
		// this is because someone can upload something bad and then delete it before admin sees :(
		await auditLogService.Add(
			category: AuditLogService.Categories.ContentDelete,
			message: "Content deleted",
			username: user.UserName,
			userId: user.Id,
			data: new()
			{
				["contentId"] = content.Id.ToString(CultureInfo.InvariantCulture),
				["contentTitle"] = content.Title
			},
			token: CancellationToken.None
		);

		dbContext.Contents.Remove(content);
		await dbContext.SaveChangesAsync(token);

		var request = new DeleteObjectRequest
		{
			BucketName = config.GetRequiredValue("S3:BucketName"),
			Key = id.ToString(CultureInfo.InvariantCulture)
		};

		var result = await s3Client.DeleteObjectAsync(request, token);
		if ((int)result.HttpStatusCode >= 400)
		{
			ErrorMessage = "Failed to remove content from cdn";
			logger.LogError("Failed to remove content ({Id}) from cdn: {StatusCode} {Metadata}", 
				id, 
				result.HttpStatusCode,
				result.ResponseMetadata);
			return;
		}

		NotFound = true;
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
