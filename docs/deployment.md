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
S3-compatible storage. Application-level encryption tetap wajib. V1 memakai
KEK 32 byte yang diinjeksi lewat environment/secret manager dan dirotasi dengan
identifier versi; integrasi AWS KMS, Azure Key Vault, Google Cloud KMS, atau
Vault merupakan opsi provider masa depan, bukan kemampuan V1 yang diklaim.

## Railway shared-monorepo deployment

Buat layanan API dan worker terpisah dari root repository. Tetapkan config path
API ke `/railway.toml` dan config path worker ke `/railway.worker.toml`. Jangan
tetapkan root directory layanan ke `/backend`, karena kedua build memakai file
`Directory.*` dan `global.json` dari root repository.

Injeksikan `ASPNETCORE_ENVIRONMENT=Production`, `SEED_DEMO_DATA=false`,
`CLAMAV_FAIL_CLOSED=true`, variabel koneksi database/object storage/ClamAV,
`PUBLIC_APP_URL`, `FRONTEND_URL`, dan `CORS_ALLOWED_ORIGINS` HTTPS yang eksplisit,
serta secret 32 byte terpisah untuk `FILE_ENCRYPTION_KEK` dan
`PRIVACY_IP_HASH_KEY`. Layanan API saja tidak lengkap: worker harus memakai
PostgreSQL, object storage, ClamAV, dan KEK yang sama.

Semua nilai tersebut diatur melalui dashboard Railway atau secret manager dan
sengaja tidak ditulis ke config-as-code di repository.

## Production checklist

- HTTPS/HSTS, explicit HTTPS CORS, Secure/HttpOnly cookie, debug off.
- `SEED_DEMO_DATA=false`, Swagger off, health detail private.
- KEK dan privacy HMAC berbeda, kuat, versioned, dan punya recovery process.
- PostgreSQL/object storage private; ClamAV fail-closed; SMTP valid.
- Quota, request size, rate limit, retention, and worker interval ditinjau.
- Backup encrypted dan restore drill berhasil; log redaction diverifikasi.
- Dependency/CodeQL/container workflows lulus dan security contact valid.

Jangan deploy nyata tanpa domain, certificate, production secret, database,
object storage, dan SMTP credential milik operator.
