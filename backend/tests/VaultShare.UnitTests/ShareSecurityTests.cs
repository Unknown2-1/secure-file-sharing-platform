using VaultShare.Domain.Shares;
using VaultShare.Infrastructure.Shares;

namespace VaultShare.UnitTests;

public sealed class ShareSecurityTests
{
    [Fact]
    public void Tokens_are_high_entropy_url_safe_and_verified_by_hash()
    {
        var generator = new SecureTokenGenerator();
        var tokens = Enumerable.Range(0, 100).Select(_ => generator.Generate(32)).ToArray();
        Assert.Equal(100, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.All(tokens, token =>
        {
            Assert.True(token.Length >= 43);
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
            var hash = generator.Hash(token);
            Assert.NotEqual(token, hash);
            Assert.True(generator.Verify(token, hash));
            Assert.False(generator.Verify($"{token}x", hash));
        });
    }

    [Fact]
    public void Share_lifecycle_enforces_start_expiry_revoke_and_one_time_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var share = new Share(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "public", "hash", "idempotency",
            "name", null, now.AddMinutes(1), now.AddHours(1), null, true, false, now);
        Assert.False(share.TryReserveDownload(now));
        Assert.True(share.TryReserveDownload(now.AddMinutes(2)));
        Assert.False(share.TryReserveDownload(now.AddMinutes(3)));
        share.Revoke(Guid.NewGuid(), now.AddMinutes(4));
        Assert.False(share.CanAccess(now.AddMinutes(5)));
    }
}
