using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Amazon.S3.Model;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Estral.Host.Web.Features.Pages.Content.Upload;


[Authorize]
[AutoValidateAntiforgeryToken]
public class UploadModel : PageModel
{

	public string? ErrorMessage { get; private set; }
	public IList<string>? Errors { get; private set; }

	public void OnGet()
	{
	}

	public async Task OnPost(
		[FromForm] FormModel model,
		[FromServices] Database.AppDbContext dbContext,
		[FromServices] UserManager<Database.User> userManager,
		[FromServices] Amazon.S3.IAmazonS3 s3Client,
		[FromServices] IConfiguration config,
		[FromServices] ILogger<UploadModel> logger,
		CancellationToken token)
	{

		var title = StringExtensions.NormalizeToNull(model.Title.Trim());
		var description = StringExtensions.NormalizeToNull(model.Description?.Trim());

		if (title is null)
		{
			ErrorMessage = "Title cannot be empty or only whitespace";
			return;
		}

		var user = await userManager.GetUserAsync(User) ?? throw new Exception("Null authorized user??");

		if (await dbContext.Contents.AnyAsync(m => m.Owner.Id == user.Id && m.Title == model.Title, token))
		{
			ErrorMessage = "Duplicate title";
			return;
		}

		var content = Database.Content.Create(title, description, user);
		await dbContext.Contents.AddAsync(content, token);
		await dbContext.SaveChangesAsync(token);

		var ms = new MemoryStream();
		await model.File.CopyToAsync(ms, token);

		var request = new PutObjectRequest
		{
			BucketName = config.GetRequiredValue("S3:BucketName"),
			Key = content.Id.ToString(CultureInfo.InvariantCulture),
			ContentType = model.File.ContentType,
			InputStream = ms,
			DisablePayloadSigning = true, // needed for r2
		};

		model.File = null!;

		var result = await s3Client.PutObjectAsync(request, token);

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

	public sealed class FormModel
	{

		[Required]
		public IFormFile File { get; set; } = null!;

		[Required]
		public string Title { get; set; } = null!;

		public string? Description { get; set; }
	}
}
