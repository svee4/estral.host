using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Features.Pages.User;

public class UserModel : PageModel
{

	[MemberNotNullWhen(false, nameof(UserData))]
	public new bool NotFound { get; private set; }

	public UserDataDto? UserData { get; private set; }

	public async Task OnGet(
		int id,
		[FromServices] Database.AppDbContext dbContext)
	{
		var user = await dbContext.Users.Where(m => m.Id == id).FirstOrDefaultAsync();
		if (user is null)
		{
			NotFound = true;
			return;
		}

		UserData = new UserDataDto()
		{
			Id = user.Id,
			UserName = user.UserName,
			ProfileDescription = user.ProfileDescription,
		};
	}

	public class UserDataDto
	{
		public required int Id { get; init; }
		public required string UserName { get; init; }
		public required string? ProfileDescription { get; init; }
	}
}
