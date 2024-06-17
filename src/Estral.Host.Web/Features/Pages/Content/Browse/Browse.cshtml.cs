using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Features.Pages.Content.Browse;

public class BrowseModel : PageModel
{

	public IList<ContentDto> Contents { get; private set; } = null!;

	public async Task OnGet(
		[FromQuery] Query q,
		[FromServices] Database.AppDbContext dbContext
	)
	{
		IQueryable<Database.Content> query = dbContext.Contents
			.Include(m => m.Owner)
			.OrderByDescending(m => m.Created);

		if (q.UserId is { } userId)
		{
			query = query.Where(m => m.Owner.Id == userId);
		}

		Contents = await query
			.Select(m => new ContentDto()
			{
				Id = m.Id,
				Title = m.Title,
				OwnerId = m.Owner.Id,
				OwnerName = m.Owner.UserName,
			})
			.ToListAsync();
	}


	public sealed class Query
	{
		public int? UserId { get; set; }
	}

	public class ContentDto
	{
		public required int Id { get; init; }
		public required string Title { get; init; }
		public required string OwnerName { get; init; }
		public required int OwnerId { get; init; }
	}
}
