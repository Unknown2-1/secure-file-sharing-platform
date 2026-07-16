# Production Hardening

- HTTPS/HSTS and Secure HttpOnly cookies; specific CORS only.
- Strong managed database credentials and private networks.
- Private bucket, ClamAV fail closed, quota/rate/body/header limits.
- KMS-managed KEK, tested rotation/restore, encrypted backups.
- Swagger/debug/health detail disabled or protected.
- Redacted logs, incident contact, dependency update cadence, resource limits.

