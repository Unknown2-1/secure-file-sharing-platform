# Deployment

## Single VPS

1. Pasang Docker Engine/Compose, firewall, dan TLS reverse proxy.
2. Buat secret production di secret manager/file root-only di luar repository.
3. Gunakan password PostgreSQL/MinIO kuat, private network, dan bucket tanpa
   anonymous access.
4. Jalankan migration sebagai one-shot API `--migrate-only` sebelum rollout.
5. Jalankan API dan worker terpisah; ClamAV harus ready dan fail-closed.
6. Arahkan SMTP terverifikasi dan batasi akses Mailpit hanya untuk Development.
7. Expose hanya proxy HTTPS. Jangan expose PostgreSQL, MinIO, ClamAV, worker,
   Swagger, atau detailed readiness ke Internet.
8. Terapkan CPU/memory/disk limit, alert job failure, serta encrypted backup.

`docker-compose.production.example.yml` hanya overlay contoh, bukan secret
management. External TLS proxy wajib mempertahankan `X-Forwarded-Proto`; Nginx
internal meneruskannya dan API memakai forwarded-header support. Verifikasi
Secure cookie dan HSTS dari sisi browser setelah TLS termination.

## Managed services

PostgreSQL dapat diganti managed PostgreSQL dan MinIO dengan private
S3-compatible storage. Application-level encryption tetap wajib. KEK harus dari
AWS KMS, Azure Key Vault, Google Cloud KMS, atau Vault melalui implementasi
`IKeyEncryptionProvider`; local environment provider hanya untuk self-hosted
deployment yang secret injection-nya terlindungi.

## Production checklist

- HTTPS/HSTS, explicit HTTPS CORS, Secure/HttpOnly cookie, debug off.
- `SEED_DEMO_DATA=false`, Swagger off, health detail private.
- KEK dan privacy HMAC berbeda, kuat, versioned, dan punya recovery process.
- PostgreSQL/object storage private; ClamAV fail-closed; SMTP valid.
- Quota, request size, rate limit, retention, and worker interval ditinjau.
- Backup encrypted dan restore drill berhasil; log redaction diverifikasi.
- Dependency/CodeQL/container workflows lulus dan security contact diganti.

Jangan deploy nyata tanpa domain, certificate, production KMS/secret, database,
object storage, dan SMTP credential milik operator.
