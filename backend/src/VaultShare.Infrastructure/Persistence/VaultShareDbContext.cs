using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VaultShare.Domain.Auditing;
using VaultShare.Domain.Files;
using VaultShare.Domain.Notifications;
using VaultShare.Domain.Shares;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.Infrastructure.Persistence;

public sealed class VaultShareDbContext(DbContextOptions<VaultShareDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<FileUpload> FileUploads => Set<FileUpload>();

    public DbSet<FileEncryptionMetadata> FileEncryptionMetadata => Set<FileEncryptionMetadata>();

    public DbSet<MalwareScanRecord> MalwareScanResults => Set<MalwareScanRecord>();

    public DbSet<Share> Shares => Set<Share>();
    public DbSet<ShareItem> ShareItems => Set<ShareItem>();
    public DbSet<PublicDownloadSession> DownloadSessions => Set<PublicDownloadSession>();
    public DbSet<FileDownload> FileDownloads => Set<FileDownload>();
    public DbSet<ShareAccessAttempt> ShareAccessAttempts => Set<ShareAccessAttempt>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<InternalFileGrant> InternalFileGrants => Set<InternalFileGrant>();
    public DbSet<WorkspaceSetting> WorkspaceSettings => Set<WorkspaceSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
            entity.HasIndex(user => user.DeletedAt);
        });

        builder.Entity<UserSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.Property(session => session.UserAgent).HasMaxLength(256);
            entity.Property(session => session.IpAddressHash).HasMaxLength(64);
            entity.HasIndex(session => new { session.UserId, session.RevokedAt });
            entity.HasIndex(session => session.ExpiresAt);
        });

        builder.Entity<Workspace>(entity =>
        {
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.Name).HasMaxLength(120).IsRequired();
            entity.Property(workspace => workspace.Version).IsConcurrencyToken();
            entity.HasIndex(workspace => workspace.CreatedByUserId);
            entity.HasIndex(workspace => workspace.CreatedAt);
            entity.HasIndex(workspace => workspace.DeletedAt);
        });

        builder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(member => new { member.WorkspaceId, member.UserId });
            entity.HasOne<Workspace>().WithMany().HasForeignKey(member => member.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(member => member.UserId);
            entity.HasIndex(member => new { member.WorkspaceId, member.Role });
        });

        builder.Entity<WorkspaceInvitation>(entity =>
        {
            entity.HasKey(invitation => invitation.Id);
            entity.Property(invitation => invitation.NormalizedEmail).HasMaxLength(254).IsRequired();
            entity.Property(invitation => invitation.SecretTokenHash).HasMaxLength(64).IsRequired();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(invitation => invitation.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(invitation => new { invitation.WorkspaceId, invitation.NormalizedEmail });
            entity.HasIndex(invitation => invitation.ExpiresAt);
            entity.HasIndex(invitation => invitation.SecretTokenHash);
        });

        builder.Entity<StoredFile>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("CK_StoredFiles_FileSize_Positive", "\"FileSize\" > 0"));
            entity.HasKey(file => file.Id);
            entity.Property(file => file.OriginalFilename).HasMaxLength(255).IsRequired();
            entity.Property(file => file.SafeDisplayFilename).HasMaxLength(255).IsRequired();
            entity.Property(file => file.StoredObjectKey).HasMaxLength(180).IsRequired();
            entity.Property(file => file.ClientMimeType).HasMaxLength(127).IsRequired();
            entity.Property(file => file.DetectedMimeType).HasMaxLength(127);
            entity.Property(file => file.Sha256Hash).HasMaxLength(64);
            entity.HasOne<Workspace>().WithMany().HasForeignKey(file => file.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(file => file.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(file => file.WorkspaceId);
            entity.HasIndex(file => file.OwnerUserId);
            entity.HasIndex(file => file.StoredObjectKey).IsUnique();
            entity.HasIndex(file => file.CreatedAt);
            entity.HasIndex(file => file.DeletedAt);
            entity.HasIndex(file => file.PurgedAt);
            entity.HasIndex(file => file.UploadStatus);
            entity.HasIndex(file => file.MalwareScanStatus);
            entity.HasIndex(file => file.EncryptionStatus);
        });

        builder.Entity<FileUpload>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_FileUploads_ExpectedSize_Positive", "\"ExpectedSize\" > 0");
                table.HasCheckConstraint("CK_FileUploads_Offset_Range", "\"UploadOffset\" >= 0 AND \"UploadOffset\" <= \"ExpectedSize\"");
                table.HasCheckConstraint("CK_FileUploads_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
            });
            entity.HasKey(upload => upload.Id);
            entity.Property(upload => upload.TemporaryPath).HasMaxLength(1024).IsRequired();
            entity.Property(upload => upload.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(upload => upload.Version).IsConcurrencyToken();
            entity.HasOne(upload => upload.StoredFile).WithOne().HasForeignKey<FileUpload>(upload => upload.StoredFileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Workspace>().WithMany().HasForeignKey(upload => upload.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(upload => upload.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(upload => new { upload.UserId, upload.IdempotencyKey }).IsUnique();
            entity.HasIndex(upload => upload.WorkspaceId);
            entity.HasIndex(upload => upload.Status);
            entity.HasIndex(upload => upload.ExpiresAt);
        });

        builder.Entity<FileEncryptionMetadata>(entity =>
        {
            entity.HasKey(metadata => metadata.Id);
            entity.Property(metadata => metadata.Algorithm).HasMaxLength(64).IsRequired();
            entity.Property(metadata => metadata.KeyProvider).HasMaxLength(128).IsRequired();
            entity.Property(metadata => metadata.KeyIdentifier).HasMaxLength(128).IsRequired();
            entity.Property(metadata => metadata.WrappedDataKey).IsRequired();
            entity.Property(metadata => metadata.KeyWrapNonce).IsRequired();
            entity.Property(metadata => metadata.KeyWrapAuthenticationTag).IsRequired();
            entity.Property(metadata => metadata.BaseNonce).IsRequired();
            entity.HasIndex(metadata => metadata.KeyIdentifier);
            entity.HasIndex(metadata => metadata.CreatedAt);
        });

        builder.Entity<StoredFile>()
            .HasOne<FileEncryptionMetadata>()
            .WithOne()
            .HasForeignKey<StoredFile>(file => file.EncryptionMetadataId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<MalwareScanRecord>(entity =>
        {
            entity.HasKey(result => result.Id);
            entity.Property(result => result.Scanner).HasMaxLength(64).IsRequired();
            entity.Property(result => result.SafeSignature).HasMaxLength(120);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(result => result.StoredFileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(result => result.StoredFileId);
            entity.HasIndex(result => result.Status);
            entity.HasIndex(result => result.ScannedAt);
        });

        builder.Entity<Share>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Shares_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
                table.HasCheckConstraint("CK_Shares_DownloadCount", "\"DownloadCount\" >= 0");
                table.HasCheckConstraint("CK_Shares_MaximumDownloads", "\"MaximumDownloads\" IS NULL OR \"MaximumDownloads\" > 0");
            });
            entity.HasKey(share => share.Id);
            entity.Property(share => share.PublicIdentifier).HasMaxLength(32).IsRequired();
            entity.Property(share => share.SecretTokenHash).HasMaxLength(64).IsRequired();
            entity.Property(share => share.CreationIdempotencyKeyHash).HasMaxLength(64).IsRequired();
            entity.Property(share => share.Name).HasMaxLength(120).IsRequired();
            entity.Property(share => share.Description).HasMaxLength(1000);
            entity.Property(share => share.PasswordHash).HasMaxLength(1024);
            entity.Property(share => share.Version).IsConcurrencyToken();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(share => share.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(share => share.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(share => share.WorkspaceId);
            entity.HasIndex(share => share.PublicIdentifier).IsUnique();
            entity.HasIndex(share => share.SecretTokenHash);
            entity.HasIndex(share => new { share.CreatedByUserId, share.CreationIdempotencyKeyHash }).IsUnique();
            entity.HasIndex(share => share.ExpiresAt);
            entity.HasIndex(share => share.IsRevoked);
            entity.HasIndex(share => share.CreatedAt);
        });

        builder.Entity<ShareItem>(entity =>
        {
            entity.HasKey(item => new { item.ShareId, item.StoredFileId });
            entity.HasOne<Share>().WithMany().HasForeignKey(item => item.ShareId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(item => item.StoredFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.StoredFileId);
        });

        builder.Entity<PublicDownloadSession>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("CK_DownloadSessions_Expiry", "\"ExpiresAt\" > \"CreatedAt\""));
            entity.HasKey(session => session.Id);
            entity.Property(session => session.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasOne<Share>().WithMany().HasForeignKey(session => session.ShareId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.HasIndex(session => new { session.ShareId, session.ExpiresAt });
        });

        builder.Entity<FileDownload>(entity =>
        {
            entity.HasKey(download => download.Id);
            entity.HasOne<Share>().WithMany().HasForeignKey(download => download.ShareId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(download => download.StoredFileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PublicDownloadSession>().WithMany().HasForeignKey(download => download.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(download => download.ShareId);
            entity.HasIndex(download => download.StoredFileId);
            entity.HasIndex(download => download.StartedAt);
        });

        builder.Entity<ShareAccessAttempt>(entity =>
        {
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.ResultCode).HasMaxLength(64).IsRequired();
            entity.Property(attempt => attempt.IpAddressHash).HasMaxLength(64).IsRequired();
            entity.Property(attempt => attempt.UserAgent).HasMaxLength(256).IsRequired();
            entity.HasOne<Share>().WithMany().HasForeignKey(attempt => attempt.ShareId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(attempt => new { attempt.ShareId, attempt.AttemptedAt });
            entity.HasIndex(attempt => attempt.IpAddressHash);
        });

        builder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.TargetType).HasMaxLength(64).IsRequired();
            entity.Property(audit => audit.TargetId).HasMaxLength(64);
            entity.Property(audit => audit.IpAddressHash).HasMaxLength(64).IsRequired();
            entity.Property(audit => audit.UserAgent).HasMaxLength(256).IsRequired();
            entity.Property(audit => audit.CorrelationId).HasMaxLength(64).IsRequired();
            entity.Property(audit => audit.Result).HasMaxLength(32).IsRequired();
            entity.Property(audit => audit.SafeMetadataJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(audit => new { audit.WorkspaceId, audit.Timestamp });
            entity.HasIndex(audit => audit.ActorUserId);
            entity.HasIndex(audit => audit.Timestamp);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Type).HasMaxLength(64).IsRequired();
            entity.Property(notification => notification.Title).HasMaxLength(160).IsRequired();
            entity.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(notification => notification.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
            entity.HasIndex(notification => new { notification.EmailRequested, notification.EmailSentAt, notification.EmailAttempts });
        });

        builder.Entity<InternalFileGrant>(entity =>
        {
            entity.HasKey(grant => grant.Id);
            entity.HasOne<StoredFile>().WithMany().HasForeignKey(grant => grant.StoredFileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(grant => grant.GrantedToUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(grant => grant.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(grant => new { grant.StoredFileId, grant.GrantedToUserId, grant.RevokedAt });
            entity.HasIndex(grant => new { grant.GrantedToUserId, grant.ExpiresAt });
        });

        builder.Entity<WorkspaceSetting>(entity =>
        {
            entity.HasKey(setting => setting.WorkspaceId);
            entity.Property(setting => setting.Version).IsConcurrencyToken();
            entity.HasOne<Workspace>().WithOne().HasForeignKey<WorkspaceSetting>(setting => setting.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(setting => setting.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_WorkspaceSettings_Quota", "\"StorageQuotaBytes\" >= 1048576");
                table.HasCheckConstraint("CK_WorkspaceSettings_AuditRetention", "\"AuditRetentionDays\" BETWEEN 30 AND 3650");
                table.HasCheckConstraint("CK_WorkspaceSettings_DeleteGrace", "\"DeletedFileGraceDays\" BETWEEN 0 AND 365");
            });
        });
    }
}
