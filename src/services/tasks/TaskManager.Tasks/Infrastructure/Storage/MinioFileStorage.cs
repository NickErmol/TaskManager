using Amazon.S3;
using Amazon.S3.Model;
using TaskManager.Tasks.Application.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Storage;

/// <summary>
/// IFileStorage backed by an S3-compatible store (MinIO locally). Path-style addressing is
/// required for MinIO; checksum calculation is forced to WHEN_REQUIRED so newer AWS SDK
/// integrity headers don't trip self-hosted MinIO.
/// </summary>
public class MinioFileStorage(IAmazonS3 s3, string bucket) : IFileStorage
{
    public async Task PutAsync(string key, Stream content, string contentType, long length, CancellationToken ct = default)
    {
        var req = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true, // http MinIO; avoids streaming-signature overhead
        };
        req.Headers.ContentLength = length;
        await s3.PutObjectAsync(req, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var resp = await s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, ct);
        return resp.ResponseStream; // caller disposes
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, ct);
}
