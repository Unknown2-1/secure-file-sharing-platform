# Testing

Checkpoint lokal terverifikasi dicatat di `STATUS.md`.

```bash
dotnet format backend/VaultShare.sln --verify-no-changes --no-restore
dotnet build backend/VaultShare.sln -c Release --no-restore
dotnet test backend/VaultShare.sln -c Release --no-build -m:1
cd frontend
npm run lint && npm run typecheck && npm test && npm run build
```

Unit tests menguji AES-GCM chunk authentication, nonce/key behavior, key wrap
dan rewrap, token entropy/hash, state machine file, serta authorization rules.
Integration tests memakai real ASP.NET Core test host dengan database/storage
fake terisolasi dan meliputi identity, CSRF, roles, resumable upload, scan
outcomes, ciphertext round trip, purge, internal/public share, atomic
one-time/max-download, audit redaction, notification, retention, dan seeder.
Security tests menguji headers, CORS, rate limit, dan safe correlation details.

Full-stack browser suite membutuhkan Docker:

```bash
cp .env.example .env                 # ganti dua placeholder key
docker compose up -d --build --wait
cd frontend
npx playwright install chromium
npm run test:e2e
```

Playwright memakai browser context penerima terpisah, password salah/benar,
download bytes, batas download, revoke, viewer denial, serta mobile viewport.
Artefak failure berada di `output/playwright/` dan tidak masuk Git. Workflow
`integration-e2e.yml` menjalankan skenario tersebut di runner Docker. Local
machine saat penulisan tidak memiliki Docker sehingga hasil runtime E2E tidak
boleh dianggap lulus hanya dari `--list`.

Generator benchmark:

```bash
infrastructure/scripts/generate-benchmark-file.sh 50
```

Jangan commit output benchmark. Uji 250 MiB hanya ketika kapasitas host cukup.
