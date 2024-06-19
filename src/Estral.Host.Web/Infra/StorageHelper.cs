using Estral.Host.Web.Infra.Extensions;

namespace Estral.Host.Web.Infra;

public sealed class StorageHelper(IConfiguration configuration)
{
	private readonly string _cdnUrl = configuration.GetRequiredValue("S3:CdnUrl");

	public string GetCdnUrl(int contentId)
	{
		var path = $"https://{_cdnUrl}/{contentId}";
		return path;
	}

	public string GetCdnPfpUrl(int userId)
	{
		var path = $"https://{_cdnUrl}/pfp/{userId}";
		return path;
	}

}
