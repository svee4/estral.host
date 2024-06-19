using Amazon.S3;
using Amazon.S3.Model;
using Estral.Host.Web.Infra.Extensions;

namespace Estral.Host.Web.Infra;

/// <summary>
/// Cloudflare R2 client
/// </summary>
public sealed class R2Client(IAmazonS3 s3Client, HttpClient httpClient, IConfiguration config)
{
	private readonly IAmazonS3 _s3Client = s3Client;
	private readonly HttpClient _httpClient = httpClient;
	private readonly IConfiguration _config = config;
	private readonly string _bucketName = config.GetRequiredValue("S3:BucketName");

	public async Task<PutObjectResponse> UploadObject(
		string key,
		string contentType,
		Stream inputStream,
		CancellationToken token)
	{
		var request = new PutObjectRequest
		{
			BucketName = _bucketName,
			Key = key,
			ContentType = contentType,
			InputStream = inputStream,
			DisablePayloadSigning = true, // needed for r2
		};

		return await _s3Client.PutObjectAsync(request, token);
	}

	public async Task<DeleteObjectResponse> DeleteObject(
		string key,
		CancellationToken token)
	{
		var request = new DeleteObjectRequest
		{
			BucketName = _bucketName,
			Key = key
		};

		return await _s3Client.DeleteObjectAsync(request, token);
	}

	public async Task InvalidateCache(
		string key,
		CancellationToken token)
	{
		var zoneId = _config.GetRequiredValue("Cloudflare:ZoneId");
		var requestUrl = $"https://api.cloudflare.com/client/v4/zones/{zoneId}/purge_cache";


		// https://community.cloudflare.com/t/purge-cache-of-r2-object/540785/4
		/*
{
	"files": 
	[
		"https://files.site.com/file.js",
		{
			"url": "https://files.site.com/file.js",
			"headers": {
				"Origin": "https://site.com"
			}
		}
	]
}
		 */


		var url = $"https://{_config.GetRequiredValue("S3:CdnUrl")}/{key}";

		var request = new Dictionary<string, string[]>
		{
			["files"] = [url]
		};

		var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
		requestMessage.Content = JsonContent.Create(request);

		var cfToken = _config.GetRequiredValue("Cloudflare:CachePurgeApiToken");
		requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfToken);

		var response = await _httpClient.SendAsync(requestMessage, token);
		var br = 1;
	}


	private sealed class PurgeCacheRequest
	{
		public Dictionary<string, File> Files { get; set; }

		public sealed class File
		{
			public string Url { get; set; }
			public Dictionary<string, string> Headers { get; set; }
		}
	}
}
