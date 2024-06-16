using Amazon.S3;
using Amazon.S3.Model;

namespace Estral.Host.Web.Infra;

public static class AmazonS3Extensions
{
	public static async Task<PutObjectResponse> UploadObject(
		this IAmazonS3 s3Client,
		string bucketName,
		string key,
		string contentType,
		Stream inputStream,
		CancellationToken token)
	{
		var request = new PutObjectRequest
		{
			BucketName = bucketName,
			Key = key,
			ContentType = contentType,
			InputStream = inputStream,
			DisablePayloadSigning = true, // needed for r2
		};

		return await s3Client.PutObjectAsync(request, token);
	}

	public static async Task<DeleteObjectResponse> DeleteObject(
		this IAmazonS3 s3Client,
		string bucketName,
		string key,
		CancellationToken token)
	{
		var request = new DeleteObjectRequest
		{
			BucketName = bucketName,
			Key = key
		};

		return await s3Client.DeleteObjectAsync(request, token);
	}
}
