using Estral.Host.Web.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Features.Api.Tags.Search;

[ApiController]
[Route("/api/tags/search")]
[Authorize]
public sealed class Search : ControllerBase
{
	public async Task<List<TagDto>> Get(
		string q,
		[FromServices] AppDbContext dbContext,
		CancellationToken token)
		=> await dbContext
			.Tags
			.Where(m => EF.Functions.ILike(m.Name, $"{q}%"))
			.Select(m => new TagDto
			{
				Id = m.Id,
				Name = m.Name
			})
			.Take(10)
			.ToListAsync(token);

	public sealed class TagDto
	{
		public required int Id { get; set; }
		public required string Name { get; set; }
	}

}
