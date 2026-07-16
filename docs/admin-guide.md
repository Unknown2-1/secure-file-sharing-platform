# Admin Guide

Owner dapat mengelola anggota, quota, audit retention, deleted-file grace,
member public-share policy, audit export, dan workspace deletion. Admin dapat
mengundang Member/Viewer dan mengelola file/share operasional, tetapi tidak bisa
menghapus Owner, mengangkat Admin baru, atau mengubah security policy.

Workspace deletion langsung menyembunyikan workspace, mencabut seluruh public
share, soft-delete file, dan mencatat audit. Ciphertext/wrapped DEK dipurge oleh
worker setelah grace period; backup mengikuti retensi terpisah.

Operational commands:

```bash
dotnet VaultShare.Api.dll --migrate-only
dotnet VaultShare.Worker.dll storage-check                 # dry-run
dotnet VaultShare.Worker.dll storage-check --delete-orphans # explicit cleanup
dotnet VaultShare.Worker.dll rewrap-keys --dry-run --batch-size 100
```

`storage-check` melakukan bounded prefix listing, melaporkan missing file IDs
dan jumlah orphan tanpa mencetak object key. Cleanup hanya berjalan dengan flag
eksplisit; untuk bucket lebih besar dari batch command, ulangi dan cocokkan juga
dengan inventory provider. Health
detail, job controls, database, MinIO, ClamAV, dan Swagger tidak boleh public.
