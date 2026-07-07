namespace TaskManager.Tasks.Application.DTOs;

/// <summary>Streamed download payload for an attachment. Caller disposes <see cref="Content"/>.</summary>
public record AttachmentContent(Stream Content, string ContentType, string FileName, long SizeBytes);
