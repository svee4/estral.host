using System.Security.Claims;

namespace Estral.Host.Web.Infra.Extensions;

public static class ClaimsPrincipalExtensions
{

	public static bool IsAuthenticated(this ClaimsPrincipal principal) =>
		principal.Identity?.IsAuthenticated ?? false;

	/// <summary>
	/// Get user id for claims principal, if one exists, otherwise null
	/// </summary>
	/// <param name="principal"></param>
	/// <returns></returns>
	public static int? GetUserId(this ClaimsPrincipal principal)
	{
		var claim = principal.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier);
		if (claim is not { Value: { } value }) return null;
		return int.TryParse(value, out var userId) ? userId : null;
	}

	/// <summary>
	/// Get username from claims principal, if one exists, othewise null
	/// </summary>
	/// <param name="principal"></param>
	/// <returns></returns>
	public static string? GetUserName(this ClaimsPrincipal principal) =>
		principal.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Name)?.Value;
}
