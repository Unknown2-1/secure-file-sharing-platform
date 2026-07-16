using Microsoft.AspNetCore.Identity;

namespace VaultShare.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
