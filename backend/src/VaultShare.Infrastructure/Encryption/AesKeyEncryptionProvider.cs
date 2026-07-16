using System.Security.Cryptography;
using System.Text;
using VaultShare.Application.Encryption;

namespace VaultShare.Infrastructure.Encryption;

public sealed class AesKeyEncryptionProvider : IKeyEncryptionProvider
{
    private readonly byte[] _keyEncryptionKey;

    public AesKeyEncryptionProvider(byte[] keyEncryptionKey, string keyIdentifier)
    {
        if (keyEncryptionKey.Length != 32) throw new ArgumentException("The KEK must contain exactly 32 bytes.", nameof(keyEncryptionKey));
        if (string.IsNullOrWhiteSpace(keyIdentifier) || keyIdentifier.Length > 128) throw new ArgumentException("A key identifier is required.", nameof(keyIdentifier));
        _keyEncryptionKey = keyEncryptionKey.ToArray();
        KeyIdentifier = keyIdentifier;
    }

    public string ProviderName => "local-environment-aes-gcm";
    public string KeyIdentifier { get; }

    public ValueTask<WrappedDataKey> WrapAsync(ReadOnlyMemory<byte> plaintextKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (plaintextKey.Length != 32) throw new ArgumentException("A DEK must contain exactly 32 bytes.", nameof(plaintextKey));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintextKey.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_keyEncryptionKey, tag.Length);
        aes.Encrypt(nonce, plaintextKey.Span, ciphertext, tag, AssociatedData());
        return ValueTask.FromResult(new WrappedDataKey(ciphertext, nonce, tag, ProviderName, KeyIdentifier, DateTimeOffset.UtcNow));
    }

    public ValueTask<byte[]> UnwrapAsync(WrappedDataKey wrappedKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(wrappedKey.KeyIdentifier), Encoding.UTF8.GetBytes(KeyIdentifier)) ||
            !string.Equals(wrappedKey.KeyProvider, ProviderName, StringComparison.Ordinal))
            throw new CryptographicException("The wrapped key belongs to another key provider or identifier.");
        if (wrappedKey.Ciphertext.Length != 32 || wrappedKey.Nonce.Length != 12 || wrappedKey.AuthenticationTag.Length != 16)
            throw new CryptographicException("Wrapped key metadata is invalid.");
        var plaintext = new byte[32];
        using var aes = new AesGcm(_keyEncryptionKey, wrappedKey.AuthenticationTag.Length);
        aes.Decrypt(wrappedKey.Nonce, wrappedKey.Ciphertext, wrappedKey.AuthenticationTag, plaintext, AssociatedData());
        return ValueTask.FromResult(plaintext);
    }

    private byte[] AssociatedData() => Encoding.UTF8.GetBytes($"{ProviderName}\n{KeyIdentifier}\nv1");
}
