# VaultShare Status

Evidence terakhir diperbarui pada **2026-07-18**.

- Project status: **V1 locally verified; GitHub Actions and release publication pending**.
- Scope V1 selesai: identity/workspace, resumable upload, fail-closed malware
  processing, chunked envelope encryption, file management, public/internal
  share, preview, download limits, audit, notifications, retention, dashboard,
  localization, dan responsive UI.
- Migration status: 14 PostgreSQL migrations berhasil diterapkan oleh stack
  Compose lokal.
- Backend: 49 passed, 0 failed, 0 skipped; Release build 0 warning/0 error.
- Frontend: 34 unit/component tests, lint dan strict typecheck lulus, production
  build menghasilkan 22 routes.
- Browser: 4/4 core E2E lulus; encrypted-pipeline smoke 1 MiB lulus 1/1 dalam
  batas 120 detik.
- Runtime: clean Compose project dan seluruh migration lulus; API dan worker
  berjalan sebagai uid 100/gid 101, volume temporary
  upload writable tanpa mode `777`, serta health live/ready lulus.
- Dependency: npm audit moderate-or-higher bersih setelah seluruh PostCSS
  di-resolve ke 8.5.19; audit NuGet tidak menemukan package rentan.
- Release gates: backend/frontend CI, clean full-stack E2E, dependency/CodeQL,
  full-history Gitleaks, Trivy filesystem, dan Trivy image scan tersedia.
- Security contact: `dnshadelio@gmail.com`.

## Batas V1

- KEK 32 byte berasal dari environment yang diinjeksi secret manager. Ini bukan
  cloud KMS; provider KMS vendor adalah opsi masa depan.
- ZIP bundle, E2EE/zero-knowledge, strict mid-stream revoke, multi-region,
  Kubernetes, dan mobile native adalah non-goal V1.
- Server tetap dapat mendekripsi file setelah authorization. Restore dan secure
  deletion tetap bergantung pada kebijakan backup/object-storage operator.

## Verification evidence — 2026-07-18

- `dotnet test backend/VaultShare.sln -c Release` — 49 passed.
- `npm test -- --run` — 34 passed setelah security contact regression test.
- `npm run lint`, `npm run typecheck`, `npm run build` — exit 0; 22 routes.
- `npm audit --omit=dev --audit-level=moderate` — 0 vulnerabilities.
- `infrastructure/scripts/verify-running-stack.sh` — exit 0.
- `npm run test:e2e` — 4 passed.
- `npm run test:performance` — 1 passed; payload 1 MiB.
- Railway API dan worker image build dari root context — exit 0.

## Remaining release operations

1. Push fast-forward ke `main` dan tunggu seluruh GitHub Actions hijau.
2. Publikasikan tag dan GitHub Release `v1.0.0`.
