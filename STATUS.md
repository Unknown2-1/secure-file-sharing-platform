# VaultShare Status

- Project status: **In progress — core v1 path implemented; runtime/portfolio hardening remains**
- Selesai: repository control files; .NET 10 solution; Next.js foundation;
  liveness/readiness; security-header baseline; Docker/Compose definitions;
  root documentation baseline; backend/frontend CI definitions; landing UI
- Sedang dikerjakan: full-stack runtime verification, visual/accessibility review, performance verification, dan portfolio screenshots
- Belum dibuat: production KMS provider, screenshots, dan verified runtime E2E; ZIP bundle sengaja ditunda
- Migration status: 14 PostgreSQL migrations generated through workspace settings; apply belum diverifikasi karena PostgreSQL/Docker tidak tersedia
- Test status: backend 49 meaningful tests passed / 0 failed / 0 skipped; frontend 8 passed / 0 failed; 4 Playwright scenarios discovered tetapi belum dijalankan
- Build status: backend+worker Release succeeded (0 warnings, 0 errors); frontend production build succeeded
- Security review status: identity/session/TOTP, CSRF/CORS, workspace isolation/settings, resumable upload, MIME inspection, fail-closed scan outcomes, authenticated chunked encryption, hash-only share secrets, password hashing, HttpOnly share sessions, atomic one-time reservation, streaming decrypt, safe preview, audit redaction, retention, secure headers, dan production guards covered; full runtime review belum selesai
- Known issue: `npm install` melaporkan 1 moderate + 1 high advisory pada dependency tree development; high advisory yang telah diidentifikasi berada pada transitive `brace-expansion` 5.0.7 dan belum memiliki patched registry release. Audit online detail ditolak runtime policy; offline cache melaporkan 0 dan tidak dianggap konklusif. Production dependencies tidak menggunakan lint glob tooling tersebut.
- Blocker: Docker/Compose tidak terpasang sehingga images, service health, Testcontainers, ClamAV, MinIO, Mailpit, dan end-to-end Compose belum dapat diverifikasi
- Git status: workspace memiliki read-only `.git` mount yang bukan repository valid; `git init`, commit, dan tag tidak dapat dilakukan
- Perintah terakhir berhasil: `dotnet test backend/VaultShare.sln -c Release --no-build -m:1` — 49 passed, exit 0
- Langkah berikutnya: jalankan Compose/ClamAV/MinIO/PostgreSQL dan empat Playwright scenarios pada host Docker; lakukan visual/accessibility dan performance verification

## Verification evidence — 2026-07-16

- `dotnet format backend/VaultShare.sln --no-restore` — exit 0
- `dotnet build backend/VaultShare.sln -c Release --no-restore -m:1` — exit 0, 0 warning, 0 error
- `dotnet test backend/VaultShare.sln -c Release --no-build -m:1` — 17 unit + 26 integration + 6 security tests passed
- `npm run lint` — exit 0
- `npm run typecheck` — exit 0
- `npm test` — 8 passed
- `npm run build` — exit 0
- `npm run test:e2e -- --list` — 4 browser scenarios discovered; runtime not executed
- `npm audit --offline --omit=dev` — 0 cached production advisories; non-conclusive without online advisory refresh
- local secret-pattern scan — only documented local/demo placeholders found
- seluruh shell script lulus `sh -n`; seluruh Compose/GitHub Actions YAML dapat diparse
- 35 project Markdown files memiliki local-link targets yang valid
- final secret scan menemukan hanya placeholder local/demo dan fixture test yang disengaja; tidak ada private-key/token-provider signature
- catatan environment: Turbopack dan Roslyn memerlukan eksekusi di luar sandbox untuk local port/named pipe; pengulangan tereskalasi lulus
- `docker compose config` — not run; Docker command unavailable
