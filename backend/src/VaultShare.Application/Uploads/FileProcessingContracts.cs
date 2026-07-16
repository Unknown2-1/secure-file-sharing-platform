namespace VaultShare.Application.Uploads;

public sealed record FileInspectionResult(bool IsAllowed, string? DetectedMimeType, string Sha256Hash,
    string ErrorCode);

public interface IFileContentInspector
{
    Task<FileInspectionResult> InspectAsync(string filename, Stream content, CancellationToken cancellationToken);
}

public interface ICompletedUploadProcessor
{
    Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken);
}
