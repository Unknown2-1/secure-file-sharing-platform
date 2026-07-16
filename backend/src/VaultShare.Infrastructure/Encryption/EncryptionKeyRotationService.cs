using System.Security.Cryptography;
using VaultShare.Application.Encryption;

namespace VaultShare.Infrastructure.Encryption;

public sealed class EncryptionKeyRotationService : IEncryptionKeyRotationService
{
    public async Task<WrappedDataKey> RewrapAsync(WrappedDataKey current, IKeyEncryptionProvider currentProvider,
        IKeyEncryptionProvider newProvider, CancellationToken cancellationToken)
    {
        var dataKey = await currentProvider.UnwrapAsync(current, cancellationToken);
        try
        {
            return await newProvider.WrapAsync(dataKey, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }
}
