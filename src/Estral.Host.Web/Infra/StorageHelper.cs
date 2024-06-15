using Estral.Host.Web.Infra.Extensions;

namespace Estral.Host.Web.Infra;

public sealed class StorageHelper(IConfiguration configuration)
{

	private readonly string _cdnUrl = configuration.GetRequiredValue("S3:CdnUrl");

	public string GetContentUrl(int contentId)
	{
		var path = $"https://{_cdnUrl}/{contentId}";
		return path;
	}

}
