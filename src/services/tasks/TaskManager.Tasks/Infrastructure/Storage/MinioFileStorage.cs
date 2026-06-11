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
        return new ResponseDisposingStream(resp);
    }

    /// <summary>
    /// Forwards reads to <see cref="Amazon.S3.Model.GetObjectResponse.ResponseStream"/> and
    /// disposes the entire <see cref="Amazon.S3.Model.GetObjectResponse"/> (which in turn
    /// disposes the inner stream) when this stream is disposed — preventing the wrapper object
    /// from leaking the underlying HTTP connection.
    /// </summary>
    private sealed class ResponseDisposingStream(Amazon.S3.Model.GetObjectResponse response) : Stream
    {
        private readonly Stream _inner = response.ResponseStream;

        public override bool CanRead  => _inner.CanRead;
        public override bool CanSeek  => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length   => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => _inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) response.Dispose(); // disposes ResponseStream too
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            response.Dispose();
            await base.DisposeAsync();
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, ct);
}
