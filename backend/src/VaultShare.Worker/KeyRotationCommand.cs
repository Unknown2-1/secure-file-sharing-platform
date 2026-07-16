using System.Security.Cryptography;
using VaultShare.Application.Encryption;
using VaultShare.Infrastructure.Encryption;

namespace VaultShare.Worker;

internal static class KeyRotationCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, IConfiguration configuration,
        string[] arguments, CancellationToken cancellationToken)
    {
        var dryRun = arguments.Contains("--dry-run", StringComparer.Ordinal);
        var batchSize = ReadBatchSize(arguments);
        var encodedKey = configuration["FILE_ENCRYPTION_KEK_NEXT"];
        var keyId = configuration["FILE_ENCRYPTION_KEY_ID_NEXT"];
        if (string.IsNullOrWhiteSpace(encodedKey) || string.IsNullOrWhiteSpace(keyId))
            throw new InvalidOperationException("FILE_ENCRYPTION_KEK_NEXT and FILE_ENCRYPTION_KEY_ID_NEXT are required.");
        byte[] key;
        try { key = Convert.FromBase64String(encodedKey); }
        catch (FormatException) { throw new InvalidOperationException("FILE_ENCRYPTION_KEK_NEXT must be valid Base64."); }
        if (key.Length != 32) throw new InvalidOperationException("FILE_ENCRYPTION_KEK_NEXT must contain exactly 32 bytes.");

        await using var scope = services.CreateAsyncScope();
        var rotation = scope.ServiceProvider.GetRequiredService<IEncryptionMetadataRotationService>();
        var newProvider = new AesKeyEncryptionProvider(key, keyId);
        CryptographicOperations.ZeroMemory(key);
        var result = await rotation.RotateBatchAsync(newProvider, batchSize, dryRun, cancellationToken);
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("KeyRotationCommand");
        logger.LogInformation("Key rotation batch completed. Candidates: {Candidates}; Rewrapped: {Rewrapped}; DryRun: {DryRun}",
            result.Candidates, result.Rewrapped, result.DryRun);
        return 0;
    }

    private static int ReadBatchSize(string[] arguments)
    {
        var index = Array.IndexOf(arguments, "--batch-size");
        if (index < 0) return 100;
        return index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out var size) && size is >= 1 and <= 1000
            ? size
            : throw new ArgumentException("--batch-size must be between 1 and 1000.");
    }
}
