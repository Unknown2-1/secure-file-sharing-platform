using System.Security.Cryptography;
using VaultShare.Application.Uploads;

namespace VaultShare.Infrastructure.Uploads;

public sealed class FileContentInspector : IFileContentInspector
{
    public async Task<FileInspectionResult> InspectAsync(string filename, Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanRead || !content.CanSeek) throw new ArgumentException("Inspection requires a readable, seekable stream.", nameof(content));
        content.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var header = new byte[16];
        var headerLength = 0;
        var containsNull = false;
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (headerLength < header.Length)
            {
                var copyLength = Math.Min(header.Length - headerLength, read);
                buffer.AsSpan(0, copyLength).CopyTo(header.AsSpan(headerLength));
                headerLength += copyLength;
            }
            containsNull |= buffer.AsSpan(0, read).Contains((byte)0);
            hash.AppendData(buffer, 0, read);
        }
        content.Position = 0;

        var extension = Path.GetExtension(filename).ToLowerInvariant();
        var detected = Detect(extension, header.AsSpan(0, headerLength), containsNull);
        var digest = Convert.ToHexString(hash.GetHashAndReset());
        return detected is null
            ? new(false, null, digest, "upload.content_type_mismatch")
            : new(true, detected, digest, string.Empty);
    }

    private static string? Detect(string extension, ReadOnlySpan<byte> header, bool containsNull) => extension switch
    {
        ".png" when header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) => "image/png",
        ".jpg" or ".jpeg" when header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) => "image/jpeg",
        ".pdf" when header.StartsWith("%PDF-"u8) => "application/pdf",
        ".webp" when header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8) => "image/webp",
        ".txt" when !containsNull => "text/plain",
        ".bin" => "application/octet-stream",
        _ => null,
    };
}
