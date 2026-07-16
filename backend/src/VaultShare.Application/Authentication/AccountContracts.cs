namespace VaultShare.Application.Authentication;

public sealed record RegisterAccountCommand(string Email, string Password, string DisplayName);

public sealed record LoginCommand(string Email, string Password);

public sealed record AccountProfile(Guid Id, string Email, string DisplayName);

public sealed record SessionInfo(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    string UserAgent,
    bool IsCurrent);

public sealed record TwoFactorSetup(string SharedKey, string AuthenticatorUri);

public sealed record TwoFactorEnabled(IReadOnlyList<string> RecoveryCodes);

public sealed record AccountResult<T>(bool Succeeded, string ErrorCode, T? Value)
{
    public static AccountResult<T> Success(T value) => new(true, string.Empty, value);

    public static AccountResult<T> Failure(string errorCode) => new(false, errorCode, default);
}
