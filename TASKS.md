# VaultShare Tasks

Checklist hanya ditandai selesai setelah hasilnya diverifikasi.

## Phase 1 — Foundation

- [x] Inspeksi workspace, OS, dan toolchain
- [x] Tetapkan versi platform dan catat ADR
- [x] Buat struktur repository dan dokumen kontrol
- [x] Inisialisasi solution ASP.NET Core dan proyek test
- [x] Inisialisasi Next.js strict TypeScript
- [x] Konfigurasi PostgreSQL, MinIO, ClamAV, dan Mailpit
- [x] Tambahkan health checks, base CI, dan container definitions
- [x] Formatter dan static analysis lulus
- [x] Backend base tests lulus
- [x] Frontend lint, typecheck, unit tests, dan build lulus
- [ ] Docker Compose sehat dari kondisi bersih

## Phase 2 — Authentication dan Workspace

- [x] Identity, cookie aman, CSRF, dan account lifecycle
- [x] TOTP 2FA, recovery code, dan session management
- [x] Workspace, invitation, membership, dan role policy
- [x] Authorization dan cross-workspace isolation tests
- [x] Registration, email verification, login, logout, dan generic password reset
- [x] Database-backed active sessions dan per-request revocation enforcement
- [x] TOTP setup/enable, recovery code generation, dan two-factor login
- [x] Personal workspace creation, resource policy, dan cross-workspace denial
- [x] PostgreSQL migration awal Identity, session, workspace, dan membership
- [x] CSRF regression dan IP-partitioned login rate-limit regression
- [x] Change password, email change verification, account deletion, resend verification
- [x] 2FA disable, recovery-code login regression, regenerate recovery codes
- [x] Invitation hash-only, member removal, dan role boundary tests

## Phase 3 — Upload Pipeline

- [x] Resumable offset/chunk upload dan idempotent finalize/cancel
- [x] Quota, filename normalization, MIME/magic-byte validation
- [x] Temporary storage dan abandoned upload cleanup worker
- [x] Upload UI accessible (multi-file/progress/cancel/retry) dan automated tests

## Phase 4 — Malware Scan dan Encryption

- [x] ClamAV streaming integration dengan fail-closed policy
- [x] Chunked AES-256-GCM envelope encryption
- [x] KEK provider, encrypted object-storage adapter, dan plaintext cleanup
- [x] Rewrap key command (dry-run/batch) dan cryptography tests

## Phase 5 — File Management

- [x] File lifecycle API, list/detail/search/filter/pagination
- [x] Soft-delete, restore, purge, quota, audit, notification

## Phase 6 — Secure Share

- [x] Public/internal share, hashed token dan password
- [x] Public expiration, password, limit, atomic one-time, preview policy, revoke
- [x] Share UI dan security tests

## Phase 7 — Download

- [x] Short-lived HttpOnly authorization session dan atomic reservation
- [x] Authenticated streaming decryption dan secure headers
- [x] Race, concurrency, audit, dan notification tests

## Phase 8 — Preview dan Bundle

- [x] Safe PNG/JPEG/WebP, text, dan sandboxed PDF preview
- [x] Unsafe preview rejection by allowlist dan tests
- [x] Evaluasi ZIP streaming; ditunda karena authenticated per-entry streaming dan abuse limits belum cukup matang

## Phase 9 — Audit, Security, dan Retention

- [x] Append-only audit views, CSV export, access attempts, dan correlation IDs
- [x] Rate limiting, abuse controls, retention/cleanup jobs
- [x] Privacy controls, safe user export, workspace/account deletion, dan bounded orphan-object tooling
- [x] Missing/orphan consistency report, explicit cleanup, dan development backup/benchmark scripts

## Phase 10 — Dashboard dan UX

- [x] Dashboard metrics/chart, activity summary, dan notification center
- [ ] Responsive UI, accessibility, localization id/en
- [ ] Empty/loading/error states

## Phase 11 — Testing dan Hardening

- [ ] Full unit, integration, security, Playwright runtime, dan race tests
- [ ] Performance, static/dependency/secret/container scans
- [x] Threat model and production guard review

## Phase 12 — Documentation dan Portfolio

- [x] Complete technical/user/admin/deployment documentation
- [x] Safe encrypted demo seed data
- [ ] Screenshots dari full-stack runtime
- [ ] Release 1.0.0 verification and GitHub readiness
