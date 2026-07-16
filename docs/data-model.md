# Data Model

Core aggregates are User/Session, Workspace/Member/Invitation, StoredFile/
FileUpload/EncryptionMetadata, Share/ShareItem/AccessAttempt/DownloadSession,
Audit/SecurityEvent/Notification, and Retention/Deletion records. Public IDs are
UUID, timestamps are UTC, workspace foreign keys are mandatory where relevant,
and optimistic concurrency protects mutable share/download counters.

```mermaid
erDiagram
  USER ||--o{ WORKSPACE_MEMBER : joins
  WORKSPACE ||--o{ WORKSPACE_MEMBER : contains
  WORKSPACE ||--o{ STORED_FILE : owns
  STORED_FILE ||--|| FILE_ENCRYPTION_METADATA : protects
  WORKSPACE ||--o{ SHARE : publishes
  SHARE ||--o{ SHARE_ITEM : includes
  STORED_FILE ||--o{ SHARE_ITEM : referenced_by
  STORED_FILE ||--o{ INTERNAL_FILE_GRANT : grants
  SHARE ||--o{ DOWNLOAD_SESSION : reserves
  USER ||--o{ NOTIFICATION : receives
  WORKSPACE ||--o{ AUDIT_EVENT : records
  WORKSPACE ||--o| WORKSPACE_SETTING : configures
```

PostgreSQL migrations mencakup Identity/session/workspace/invitation, upload dan
file lifecycle, encryption metadata, malware scan result, secure share/download,
access attempts, append-only audit, notification, internal grants, retention
state, dan workspace settings. Constraints memvalidasi ukuran/offset/expiry,
download limits, quota, dan retention range. Migration apply tetap harus
diverifikasi pada runtime PostgreSQL nyata.
