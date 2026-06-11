namespace TaskManager.Tasks.Domain.Entities;

/// <summary>
/// A file attached to a <see cref="TaskItem"/>. Independent child collection: mutations are
/// last-write-wins and deliberately do NOT advance the parent task's RowVersion, so two
/// members attaching at once never conflict (mirrors ChecklistItem, spec §13.5).
/// </summary>
public class Attachment
{
    public Guid Id { get; private set; }
    public Guid TaskItemId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public Guid UploadedById { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    private Attachment() { }

    public static Attachment Create(
        Guid taskItemId, string fileName, string contentType, long sizeBytes, string storageKey, Guid uploadedById)
        => new()
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskItemId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            UploadedById = uploadedById,
            UploadedAt = DateTimeOffset.UtcNow,
        };
}
