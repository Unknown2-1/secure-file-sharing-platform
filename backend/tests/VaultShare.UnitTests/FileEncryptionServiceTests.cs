using System.Security.Cryptography;
using VaultShare.Infrastructure.Encryption;

namespace VaultShare.UnitTests;

public sealed class FileEncryptionServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(1_000_000)]
    public async Task Round_trip_supports_empty_and_chunked_streams(int length)
    {
        var plaintext = RandomNumberGenerator.GetBytes(length);
        var provider = new AesKeyEncryptionProvider(RandomNumberGenerator.GetBytes(32), "test-v1");
        var service = new ChunkedAesGcmFileEncryptionService(provider);
        await using var encrypted = new MemoryStream();

        var metadata = await service.EncryptAsync(Guid.NewGuid(), new MemoryStream(plaintext), encrypted, 64 * 1024, default);
        Assert.NotEqual(plaintext, encrypted.ToArray());
        encrypted.Position = 0;
        await using var decrypted = new MemoryStream();
        await service.DecryptAsync(ReadFileId(encrypted), encrypted, decrypted, metadata, default);
        Assert.Equal(plaintext, decrypted.ToArray());
    }

    [Fact]
    public async Task Tampering_and_wrong_kek_fail_authentication()
    {
        var fileId = Guid.NewGuid();
        var provider = new AesKeyEncryptionProvider(RandomNumberGenerator.GetBytes(32), "test-v1");
        var service = new ChunkedAesGcmFileEncryptionService(provider);
        await using var encrypted = new MemoryStream();
        var metadata = await service.EncryptAsync(fileId, new MemoryStream("confidential"u8.ToArray()), encrypted, 4096, default);
        var tampered = encrypted.ToArray();
        tampered[^17] ^= 0x40;

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => service.DecryptAsync(
            fileId, new MemoryStream(tampered), new MemoryStream(), metadata, default));

        var wrongService = new ChunkedAesGcmFileEncryptionService(
            new AesKeyEncryptionProvider(RandomNumberGenerator.GetBytes(32), "test-v1"));
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => wrongService.DecryptAsync(
            fileId, new MemoryStream(encrypted.ToArray()), new MemoryStream(), metadata, default));
    }

    [Fact]
    public async Task Each_encryption_uses_a_fresh_dek_and_nonce()
    {
        var provider = new AesKeyEncryptionProvider(RandomNumberGenerator.GetBytes(32), "test-v1");
        var service = new ChunkedAesGcmFileEncryptionService(provider);
        var fileId = Guid.NewGuid();
        await using var first = new MemoryStream();
        await using var second = new MemoryStream();
        var a = await service.EncryptAsync(fileId, new MemoryStream("same"u8.ToArray()), first, 4096, default);
        var b = await service.EncryptAsync(fileId, new MemoryStream("same"u8.ToArray()), second, 4096, default);

        Assert.NotEqual(a.BaseNonce, b.BaseNonce);
        Assert.NotEqual(a.WrappedDataKey.Ciphertext, b.WrappedDataKey.Ciphertext);
        Assert.NotEqual(first.ToArray(), second.ToArray());
    }

    [Fact]
    public async Task Reads_are_bounded_by_chunk_size_and_keys_can_be_rewrapped()
    {
        var oldProvider = new AesKeyEncryptionProvider(RandomNumberGenerator.GetBytes(32), "old");
        var newProvider = new AesKeyEncryptionProvider(RandomNumberGenerator.GetBytes(32), "new");
        var service = new ChunkedAesGcmFileEncryptionService(oldProvider);
        await using var encrypted = new MemoryStream();
        await using var source = new BoundedReadStream(RandomNumberGenerator.GetBytes(2_000_000), 256 * 1024);
        var metadata = await service.EncryptAsync(Guid.NewGuid(), source, encrypted, 256 * 1024, default);

        var rotated = await new EncryptionKeyRotationService().RewrapAsync(metadata.WrappedDataKey, oldProvider, newProvider, default);
        Assert.Equal("new", rotated.KeyIdentifier);
        Assert.Equal(await oldProvider.UnwrapAsync(metadata.WrappedDataKey, default), await newProvider.UnwrapAsync(rotated, default));
    }

    private static Guid ReadFileId(MemoryStream stream)
    {
        var bytes = stream.ToArray();
        return new Guid(bytes.AsSpan(4, 16));
    }

    private sealed class BoundedReadStream(byte[] content, int maximumRead) : MemoryStream(content)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length > maximumRead) throw new InvalidOperationException("Encryption attempted an unbounded read.");
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
