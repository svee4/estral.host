namespace Estral.Host.Web.Infra.Authorization;

public static class Roles
{
	public const string Admin = "admin";

	public static IReadOnlyList<string> AllRoles => [Admin];
}
