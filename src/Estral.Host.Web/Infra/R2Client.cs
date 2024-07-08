using Amazon.S3;
using Amazon.S3.Model;
using Estral.Host.Web.Infra.Extensions;

namespace Estral.Host.Web.Infra;

/// <summary>
/// Cloudflare R2 client
/// </summary>
public sealed class R2Client(IAmazonS3 s3Client, HttpClient httpClient, IConfiguration config, ILogger<R2Client> logger)
{
	private readonly IAmazonS3 _s3Client = s3Client;
	private readonly HttpClient _httpClient = httpClient;
	private readonly IConfiguration _config = config;
	private readonly ILogger _logger = logger;
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

	public async Task<DeleteObjectResponse> DeleteObject(string key, CancellationToken token)
	{
		var request = new DeleteObjectRequest
		{
			BucketName = _bucketName,
			Key = key
		};

		return await _s3Client.DeleteObjectAsync(request, token);
	}

	public async Task InvalidateCache(string key, CancellationToken token)
	{
		var zoneId = _config.GetRequiredValue("Cloudflare:ZoneId");
		var requestUrl = $"https://api.cloudflare.com/client/v4/zones/{zoneId}/purge_cache";

		var url = $"https://{_config.GetRequiredValue("S3:CdnUrl")}/{key}";

		var request = new Dictionary<string, string[]>
		{
			["files"] = [url]
		};

		using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
		{
			Content = JsonContent.Create(request)
		};

		var cfToken = _config.GetRequiredValue("Cloudflare:CachePurgeApiToken");
		requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfToken);

		var response = await _httpClient.SendAsync(requestMessage, token);
		if (!response.IsSuccessStatusCode)
		{
			var content = await response.Content.ReadAsStringAsync(token);
			_logger.LogError("Failed to purge R2 cache for key {Key}: {Response}", key, content);
		}
	}
}
