# Production Hardening

- HTTPS/HSTS and Secure HttpOnly cookies; specific CORS only.
- Strong managed database credentials and private networks.
- Private bucket, ClamAV fail closed, quota/rate/body/header limits.
- Environment-backed 32-byte KEK from a secret manager, tested rotation/restore,
  and encrypted backups. A cloud-KMS provider is a future option, not a V1 claim.
- Swagger/debug/health detail disabled or protected.
- Redacted logs, incident contact, dependency update cadence, resource limits.
