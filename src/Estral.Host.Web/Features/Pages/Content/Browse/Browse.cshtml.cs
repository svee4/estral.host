using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Features.Pages.Content.Browse;

public class BrowseModel : PageModel
{

	public IList<ContentDto> Contents { get; private set; } = null!;
	public QueryInfoDto QueryInfo { get; private set; } = null!;

	public async Task OnGet(
		[FromQuery] Query q,
		[FromServices] Database.AppDbContext dbContext,
		CancellationToken token)
	{
		IQueryable<Database.Content> query = dbContext.Contents
			.Include(m => m.Owner)
			.OrderByDescending(m => m.Created);

		QueryInfo = new();

		if (q.UserId is { } userId)
		{
			var user = await dbContext.Users.Where(m => m.Id ==  userId).FirstOrDefaultAsync(token);

			if (user is not null)
			{
				QueryInfo.Username = user.UserName;
				QueryInfo.UserId = user.Id;
				query = query.Where(m => m.Owner.Id == userId);
			}
		}

		if (q.Tags is { Count: > 0 } queryTags)
		{
			query = query.Where(m => m.Tags.Any(tag => queryTags.Contains(tag.Name)));
			QueryInfo.Tags = queryTags;
		}

		Contents = await query
			.Select(m => new ContentDto()
			{
				Id = m.Id,
				Title = m.Title,
				OwnerId = m.Owner.Id,
				OwnerName = m.Owner.UserName,
			})
			.ToListAsync(token);
	}


	public sealed class Query
	{
		public int? UserId { get; set; }
		public IReadOnlyList<string>? Tags { get; set; }
	}

	public sealed class ContentDto
	{
		public required int Id { get; init; }
		public required string Title { get; init; }
		public required string OwnerName { get; init; }
		public required int OwnerId { get; init; }
	}

	public sealed class QueryInfoDto
	{
		public string? Username { get; set; }
		public int? UserId { get; set; }
		public IReadOnlyList<string>? Tags { get; set; }
	}
}
