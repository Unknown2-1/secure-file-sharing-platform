# Development

Gunakan .NET SDK yang dipin `global.json` dan Node 24 LTS (container). Salin
`.env.example`, buat dua random Base64 32-byte key berbeda, dan jangan commit
`.env`. Development default memakai PostgreSQL, private MinIO bucket, ClamAV
fail-closed, Mailpit, API, worker, Next.js, serta Nginx.

```bash
cp .env.example .env
docker compose up --build --wait
```

API Development menjalankan migration dan seeder idempotent bila
`SEED_DEMO_DATA=true`. Seeder menciptakan sembilan akun role, tiga workspace,
ciphertext fixture yang dapat didekripsi, sample share, audit, dan notification.
Production guard menolak demo seed.

Manual workflow:

```bash
dotnet restore backend/VaultShare.sln --locked-mode
dotnet run --project backend/src/VaultShare.Api
dotnet run --project backend/src/VaultShare.Worker
cd frontend && npm ci && npm run dev
```

Temporary upload root harus writable hanya oleh API/worker. Jangan memakai
folder upload/object data di bawah static web root. Tambahkan keputusan teknis
baru ke `docs/architecture-decisions/`; jangan mengubah ciphertext format tanpa
version bump dan migration plan.
