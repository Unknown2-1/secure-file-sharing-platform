using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio;
using VaultShare.Application.Auditing;
using VaultShare.Application.Authentication;
using VaultShare.Application.Dashboard;
using VaultShare.Application.Encryption;
using VaultShare.Application.Files;
using VaultShare.Application.Maintenance;
using VaultShare.Application.Notifications;
using VaultShare.Application.Privacy;
using VaultShare.Application.Scanning;
using VaultShare.Application.Shares;
using VaultShare.Application.Storage;
using VaultShare.Application.Uploads;
using VaultShare.Application.Workspaces;
using VaultShare.Domain.Shares;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Auditing;
using VaultShare.Infrastructure.Authorization;
using VaultShare.Infrastructure.Dashboard;
using VaultShare.Infrastructure.Encryption;
using VaultShare.Infrastructure.Files;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Maintenance;
using VaultShare.Infrastructure.Notifications;
using VaultShare.Infrastructure.Persistence;
using VaultShare.Infrastructure.Privacy;
using VaultShare.Infrastructure.Scanning;
using VaultShare.Infrastructure.Seeding;
using VaultShare.Infrastructure.Shares;
using VaultShare.Infrastructure.Storage;
using VaultShare.Infrastructure.Uploads;
using VaultShare.Infrastructure.Workspaces;

namespace VaultShare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVaultShareInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var keyEncryptionKey = environment.IsEnvironment("Testing")
            ? RandomNumberGenerator.GetBytes(32)
            : ReadRequiredBase64Key(configuration, "FILE_ENCRYPTION_KEK");
        if (!environment.IsEnvironment("Testing")) _ = ReadRequiredBase64Key(configuration, "PRIVACY_IP_HASH_KEY");
        var keyIdentifier = environment.IsEnvironment("Testing")
            ? "test-kek"
            : configuration["FILE_ENCRYPTION_KEY_ID"] ?? throw new InvalidOperationException("FILE_ENCRYPTION_KEY_ID is required.");

        var testingDatabaseName = $"vaultshare-tests-{Guid.NewGuid():N}";
        services.AddDbContext<VaultShareDbContext>(options =>
        {
            if (environment.IsEnvironment("Testing"))
            {
                options.UseInMemoryDatabase(testingDatabaseName);
                return;
            }

            var connectionString = configuration["DATABASE_CONNECTION_STRING"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("DATABASE_CONNECTION_STRING is required.");
            }

            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3));
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<VaultShareDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = environment.IsProduction() ? "__Host-VaultShare.Auth" : "VaultShare.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.EventsType = typeof(ApplicationCookieEvents);
        });

        services.AddHttpContextAccessor();
        services.AddRouting();
        services.AddScoped<ApplicationCookieEvents>();
        services.AddScoped<IEmailService>(serviceProvider =>
            environment.IsEnvironment("Testing")
                ? serviceProvider.GetRequiredService<NullEmailService>()
                : serviceProvider.GetRequiredService<SmtpEmailService>());
        services.AddScoped<NullEmailService>();
        services.AddScoped<SmtpEmailService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IWorkspaceSettingService, WorkspaceSettingService>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<IUploadMaintenanceService, UploadMaintenanceService>();
        services.AddScoped<IFileContentInspector, FileContentInspector>();
        services.AddScoped<ICompletedUploadProcessor, CompletedUploadProcessor>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IInternalFileAccessService, InternalFileAccessService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddScoped<IPasswordHasher<Share>, PasswordHasher<Share>>();
        services.AddScoped<IShareService, ShareService>();
        services.AddScoped<IDownloadService, DownloadService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<INotificationCenterService, NotificationCenterService>();
        services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IStorageConsistencyService, StorageConsistencyService>();
        services.AddScoped<IUserDataExportService, UserDataExportService>();
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddSingleton<IKeyEncryptionProvider>(new AesKeyEncryptionProvider(keyEncryptionKey, keyIdentifier));
        services.AddSingleton<IFileEncryptionService, ChunkedAesGcmFileEncryptionService>();
        services.AddSingleton<IEncryptionKeyRotationService, EncryptionKeyRotationService>();
        services.AddScoped<IEncryptionMetadataRotationService, EncryptionMetadataRotationService>();
        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();
            services.AddSingleton<IMalwareScanner, AlwaysCleanTestScanner>();
        }
        else
        {
            var endpoint = Required(configuration, "OBJECT_STORAGE_ENDPOINT");
            var accessKey = Required(configuration, "OBJECT_STORAGE_ACCESS_KEY");
            var secretKey = Required(configuration, "OBJECT_STORAGE_SECRET_KEY");
            var bucket = Required(configuration, "OBJECT_STORAGE_BUCKET");
            var useSsl = bool.TryParse(configuration["OBJECT_STORAGE_USE_SSL"], out var secure) && secure;
            var client = new MinioClient().WithEndpoint(endpoint).WithCredentials(accessKey, secretKey).WithSSL(useSsl).Build();
            services.AddSingleton(client);
            services.AddSingleton<IObjectStorage>(new MinioObjectStorage(client, bucket));
            var clamAvHost = Required(configuration, "CLAMAV_HOST");
            var clamAvPort = int.TryParse(configuration["CLAMAV_PORT"], out var parsedPort) && parsedPort is > 0 and <= 65535
                ? parsedPort
                : throw new InvalidOperationException("CLAMAV_PORT must be a valid TCP port.");
            services.AddSingleton<IMalwareScanner>(serviceProvider => new ClamAvMalwareScanner(
                clamAvHost,
                clamAvPort,
                TimeSpan.FromMinutes(5),
                serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ClamAvMalwareScanner>>()));
        }
        services.AddScoped<IAuthorizationHandler, WorkspaceRoleAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(WorkspacePolicies.View, policy => policy.Requirements.Add(
                new WorkspaceRoleRequirement(WorkspaceRole.Owner, WorkspaceRole.Admin, WorkspaceRole.Member, WorkspaceRole.Viewer)));
            options.AddPolicy(WorkspacePolicies.Upload, policy => policy.Requirements.Add(
                new WorkspaceRoleRequirement(WorkspaceRole.Owner, WorkspaceRole.Admin, WorkspaceRole.Member)));
            options.AddPolicy(WorkspacePolicies.ManageSecurity, policy => policy.Requirements.Add(
                new WorkspaceRoleRequirement(WorkspaceRole.Owner)));
            options.AddPolicy(WorkspacePolicies.ManageMembers, policy => policy.Requirements.Add(
                new WorkspaceRoleRequirement(WorkspaceRole.Owner, WorkspaceRole.Admin)));
        });
        return services;
    }

    private static byte[] ReadRequiredBase64Key(IConfiguration configuration, string key)
    {
        var encoded = configuration[key];
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Contains("REPLACE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{key} is required and must not be a placeholder.");
        try
        {
            var value = Convert.FromBase64String(encoded);
            if (value.Length != 32) throw new FormatException();
            return value;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{key} must be a Base64-encoded 32-byte key.");
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"{key} is required.")
            : configuration[key]!;
}
