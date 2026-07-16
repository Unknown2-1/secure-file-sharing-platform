using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using VaultShare.Application.Shares;

namespace VaultShare.Infrastructure.Shares;

public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public string Generate(int byteLength)
    {
        if (byteLength < 16) throw new ArgumentOutOfRangeException(nameof(byteLength));
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteLength));
    }

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool Verify(string token, string expectedHash)
    {
        byte[] expected;
        try { expected = Convert.FromHexString(expectedHash); }
        catch (FormatException) { return false; }
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
