namespace TaskManager.Tasks.Application.Interfaces;

/// <summary>
/// Object-storage port. The MinIO/S3 adapter lives in Infrastructure so the Onion rule holds
/// (Application must not reference Infrastructure or any storage SDK).
/// </summary>
public interface IFileStorage
{
    Task PutAsync(string key, Stream content, string contentType, long length, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
