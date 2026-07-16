using System.Buffers.Binary;
using System.Security.Cryptography;
using VaultShare.Application.Encryption;

namespace VaultShare.Infrastructure.Encryption;

public sealed class ChunkedAesGcmFileEncryptionService(IKeyEncryptionProvider keyProvider) : IFileEncryptionService
{
    private static ReadOnlySpan<byte> Magic => "VSH1"u8;
    private const int AlgorithmVersion = 1;
    private const int TagSize = 16;
    private const int MinimumChunkSize = 4096;
    private const int MaximumChunkSize = 8 * 1024 * 1024;

    public async Task<FileEncryptionResult> EncryptAsync(Guid fileId, Stream plaintext, Stream ciphertext,
        int chunkSize, CancellationToken cancellationToken)
    {
        ValidateArguments(fileId, plaintext, ciphertext, chunkSize);
        var dataKey = RandomNumberGenerator.GetBytes(32);
        var baseNonce = RandomNumberGenerator.GetBytes(8);
        try
        {
            var wrapped = await keyProvider.WrapAsync(dataKey, cancellationToken);
            await WriteHeaderAsync(ciphertext, fileId, chunkSize, baseNonce, cancellationToken);
            var buffer = new byte[chunkSize];
            uint index = 0;
            var wroteChunk = false;
            while (true)
            {
                var length = await FillBufferAsync(plaintext, buffer, cancellationToken);
                if (length == 0 && wroteChunk) break;
                await EncryptChunkAsync(ciphertext, fileId, index, buffer.AsMemory(0, length), dataKey, baseNonce, cancellationToken);
                wroteChunk = true;
                if (length == 0 || length < chunkSize) break;
                if (index == uint.MaxValue) throw new CryptographicException("File has too many encryption chunks.");
                index++;
            }
            await ciphertext.FlushAsync(cancellationToken);
            return new("AES-256-GCM-CHUNKED", AlgorithmVersion, chunkSize, baseNonce, wrapped, DateTimeOffset.UtcNow);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public async Task DecryptAsync(Guid fileId, Stream ciphertext, Stream plaintext,
        FileEncryptionResult metadata, CancellationToken cancellationToken)
    {
        ValidateArguments(fileId, ciphertext, plaintext, metadata.ChunkSize);
        if (metadata.Algorithm != "AES-256-GCM-CHUNKED" || metadata.AlgorithmVersion != AlgorithmVersion || metadata.BaseNonce.Length != 8)
            throw new CryptographicException("Unsupported file encryption metadata.");
        await ValidateHeaderAsync(ciphertext, fileId, metadata, cancellationToken);
        var dataKey = await keyProvider.UnwrapAsync(metadata.WrappedDataKey, cancellationToken);
        try
        {
            uint expectedIndex = 0;
            var foundChunk = false;
            while (true)
            {
                var prefix = new byte[8];
                var prefixLength = await ReadUpToAsync(ciphertext, prefix, cancellationToken);
                if (prefixLength == 0) break;
                if (prefixLength != prefix.Length) throw new CryptographicException("Encrypted chunk prefix is truncated.");
                var index = BinaryPrimitives.ReadUInt32BigEndian(prefix.AsSpan(0, 4));
                var length = BinaryPrimitives.ReadInt32BigEndian(prefix.AsSpan(4, 4));
                if (index != expectedIndex || length < 0 || length > metadata.ChunkSize)
                    throw new CryptographicException("Encrypted chunk sequence is invalid.");
                var encrypted = new byte[length];
                var tag = new byte[TagSize];
                await ciphertext.ReadExactlyAsync(encrypted, cancellationToken);
                await ciphertext.ReadExactlyAsync(tag, cancellationToken);
                var decrypted = new byte[length];
                using (var aes = new AesGcm(dataKey, TagSize))
                {
                    aes.Decrypt(BuildNonce(metadata.BaseNonce, index), encrypted, tag, decrypted, BuildAssociatedData(fileId, index));
                }
                await plaintext.WriteAsync(decrypted, cancellationToken);
                CryptographicOperations.ZeroMemory(decrypted);
                foundChunk = true;
                if (index == uint.MaxValue) break;
                expectedIndex++;
            }
            if (!foundChunk) throw new CryptographicException("Encrypted file contains no authenticated chunks.");
            await plaintext.FlushAsync(cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static async Task EncryptChunkAsync(Stream output, Guid fileId, uint index, ReadOnlyMemory<byte> plaintext,
        byte[] dataKey, byte[] baseNonce, CancellationToken cancellationToken)
    {
        var encrypted = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(dataKey, TagSize))
        {
            aes.Encrypt(BuildNonce(baseNonce, index), plaintext.Span, encrypted, tag, BuildAssociatedData(fileId, index));
        }
        var prefix = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(prefix.AsSpan(0, 4), index);
        BinaryPrimitives.WriteInt32BigEndian(prefix.AsSpan(4, 4), plaintext.Length);
        await output.WriteAsync(prefix, cancellationToken);
        await output.WriteAsync(encrypted, cancellationToken);
        await output.WriteAsync(tag, cancellationToken);
    }

    private static async Task WriteHeaderAsync(Stream output, Guid fileId, int chunkSize, byte[] baseNonce, CancellationToken cancellationToken)
    {
        var header = new byte[36];
        Magic.CopyTo(header);
        fileId.TryWriteBytes(header.AsSpan(4, 16));
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(20, 4), AlgorithmVersion);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(24, 4), chunkSize);
        baseNonce.CopyTo(header, 28);
        await output.WriteAsync(header, cancellationToken);
    }

    private static async Task ValidateHeaderAsync(Stream input, Guid fileId, FileEncryptionResult metadata, CancellationToken cancellationToken)
    {
        var header = new byte[36];
        await input.ReadExactlyAsync(header, cancellationToken);
        if (!header.AsSpan(0, 4).SequenceEqual(Magic) || new Guid(header.AsSpan(4, 16)) != fileId ||
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)) != AlgorithmVersion ||
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(24, 4)) != metadata.ChunkSize ||
            !CryptographicOperations.FixedTimeEquals(header.AsSpan(28, 8), metadata.BaseNonce))
            throw new CryptographicException("Encrypted file header does not match its metadata.");
    }

    private static byte[] BuildNonce(ReadOnlySpan<byte> baseNonce, uint index)
    {
        var nonce = new byte[12];
        baseNonce.CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8, 4), index);
        return nonce;
    }

    private static byte[] BuildAssociatedData(Guid fileId, uint index)
    {
        var aad = new byte[24];
        fileId.TryWriteBytes(aad.AsSpan(0, 16));
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16, 4), AlgorithmVersion);
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(20, 4), index);
        return aad;
    }

    private static async Task<int> FillBufferAsync(Stream input, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await input.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static async Task<int> ReadUpToAsync(Stream input, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await input.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static void ValidateArguments(Guid fileId, Stream input, Stream output, int chunkSize)
    {
        if (fileId == Guid.Empty) throw new ArgumentException("A file ID is required.", nameof(fileId));
        if (!input.CanRead) throw new ArgumentException("Input stream must be readable.", nameof(input));
        if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));
        if (chunkSize is < MinimumChunkSize or > MaximumChunkSize) throw new ArgumentOutOfRangeException(nameof(chunkSize));
    }
}
