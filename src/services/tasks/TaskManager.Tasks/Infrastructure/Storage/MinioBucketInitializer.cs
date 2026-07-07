using Amazon.S3;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;

namespace TaskManager.Tasks.Infrastructure.Storage;

/// <summary>Ensures the attachments bucket exists before the app serves traffic.</summary>
public class MinioBucketInitializer(IAmazonS3 s3, string bucket) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3, bucket))
            await s3.PutBucketAsync(bucket, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
