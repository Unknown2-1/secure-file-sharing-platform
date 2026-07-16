using VaultShare.Domain.Files;

namespace VaultShare.UnitTests;

public sealed class StoredFileTests
{
    [Fact]
    public void Storage_commit_failure_clears_wrapped_key_reference_and_fails_closed()
    {
        var file = CreateFile();
        file.MarkUploaded();
        file.MarkValidated("text/plain", new string('a', 64));
        file.MarkScanning();
        file.MarkScanClean();
        file.MarkEncryptionStarted();
        file.MarkEncrypted(Guid.CreateVersion7());
        file.MarkAvailable(DateTimeOffset.UtcNow);

        file.MarkEncryptionStorageFailed();

        Assert.Null(file.EncryptionMetadataId);
        Assert.Equal(EncryptionStatus.Failed, file.EncryptionStatus);
        Assert.Equal(UploadStatus.Failed, file.UploadStatus);
        Assert.Equal(AvailabilityStatus.Failed, file.AvailabilityStatus);
    }

    [Fact]
    public void Object_key_is_independent_from_untrusted_filename()
    {
        var file = CreateFile("../../laporan rahasia.txt");

        Assert.DoesNotContain("laporan", file.StoredObjectKey, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("encrypted/", file.StoredObjectKey, StringComparison.Ordinal);
    }

    private static StoredFile CreateFile(string filename = "sample.txt") => new(Guid.CreateVersion7(),
        Guid.CreateVersion7(), Guid.CreateVersion7(), filename, "sample.txt",
        $"encrypted/{Guid.NewGuid():D}/{Guid.NewGuid():N}", 12, "text/plain", DateTimeOffset.UtcNow);
}
