using Estral.Host.Web.Database;
using Estral.Host.Web.Infra.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Estral.Host.Web.Features.Pages.Admin.Manage.Users;

[Authorize(Roles = Infra.Authorization.Roles.Admin)]
[ValidateAntiForgeryToken]
public class UsersModel : PageModel
{
	public List<UserDto> UsersDatas { get; private set; }

	public string? ErrorMessage { get; private set; }

	public async Task OnGet(
		[FromServices] AppDbContext dbContext,
		[FromServices] UserManager<Database.User> userManager,
		CancellationToken token)
	{
		UsersDatas = await GetUserDatas(dbContext, userManager, token);
	}

	public async Task<IActionResult> OnPostToggleLockout(
		int userId,
		[FromServices] UserManager<Database.User> userManager,
		[FromServices] AppDbContext dbContext,
		CancellationToken token)
	{
		if (userId == User.GetUserId())
		{
			UsersDatas = await GetUserDatas(dbContext, userManager, token);
			ErrorMessage = "Error: Cannot toggle own lockout state";
			return Page();
		}

		var user = await dbContext.Users.FirstOrDefaultAsync(m => m.Id == userId, token);
		if (user is null)
		{
			UsersDatas = await GetUserDatas(dbContext, userManager, token);
			ErrorMessage = "Error: No user with such id";
			return Page();
		}

		// always enable the possibility to lock out
		user.LockoutEnabled = true;

		var isLockedOut = user.LockoutEnd > DateTimeOffset.Now;
		user.LockoutEnd = isLockedOut ? null : DateTimeOffset.MaxValue;

		await dbContext.SaveChangesAsync(token);

		return LocalRedirect("/admin/manage/users");
	}

	[NonHandler]
	private static async Task<List<UserDto>> GetUserDatas(AppDbContext dbContext, UserManager<Database.User> userManager, CancellationToken token)
	{
		var data = await dbContext.Users
			.OrderByDescending(m => m.Created)
			.ToListAsync(token);

		var list = new List<UserDto>(data.Count);

		foreach (var user in data)
		{
			// TODO: parallelize tasks if needed
			var dto = new UserDto()
			{
				Id = user.Id,
				Username = user.UserName,
				Created = user.Created,
				Roles = await userManager.GetRolesAsync(user),
				LockedOut = user.LockoutEnd > DateTimeOffset.Now,
			};

			list.Add(dto);
		}

		return list;
	}

	public sealed class UserDto
	{
		public int Id { get; set; }
		public string Username { get; set; }
		public DateTimeOffset Created { get; set; }
		public IList<string> Roles { get; set; }
		public bool LockedOut { get; set; }
	}
}
