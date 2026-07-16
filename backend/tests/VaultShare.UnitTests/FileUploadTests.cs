using VaultShare.Domain.Files;

namespace VaultShare.UnitTests;

public sealed class FileUploadTests
{
    [Fact]
    public void Offset_and_lifecycle_rules_reject_sparse_or_incomplete_uploads()
    {
        var now = DateTimeOffset.UtcNow;
        var upload = new FileUpload(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "/tmp/test.upload", 10, "idempotency-key", now, now.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => upload.Advance(1, 5, now));
        upload.Advance(0, 5, now);
        Assert.Equal(5, upload.UploadOffset);
        Assert.Throws<InvalidOperationException>(() => upload.FinalizeUpload(now));

        upload.Advance(5, 5, now);
        upload.FinalizeUpload(now);
        upload.FinalizeUpload(now);
        Assert.Equal(UploadStatus.Uploaded, upload.Status);
        Assert.Equal(10, upload.UploadOffset);
    }

    [Fact]
    public void Abandon_is_idempotent_for_incomplete_upload()
    {
        var now = DateTimeOffset.UtcNow;
        var upload = new FileUpload(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "/tmp/test.upload", 10, "idempotency-key", now, now.AddHours(1));

        upload.Abandon(now);
        upload.Abandon(now.AddMinutes(1));
        Assert.Equal(UploadStatus.Abandoned, upload.Status);
    }
}
