# Disaster Recovery

Backup set harus memuat PostgreSQL (termasuk wrapped DEK dan audit), ciphertext
object storage, safe configuration, serta referensi/version KEK. Database dan
ciphertext tanpa KEK tidak cukup untuk recovery. KEK tidak boleh dimasukkan ke
repository atau dump database; backup/escrow KEK mengikuti secret-manager/KMS.

Development database backup:

```bash
infrastructure/scripts/backup-development.sh
```

Output root-only berada di `backups/` dan diabaikan Git. Production sebaiknya
memakai snapshot/PITR provider, object version/lifecycle policy, dan backup
encryption key terpisah.

## Restore drill

1. Isolasi traffic dan restore PostgreSQL ke instance baru.
2. Restore seluruh ciphertext object dengan key yang sama.
3. Restore KEK version dari KMS/secret manager; jangan menyalin melalui chat.
4. Jalankan dry-run `dotnet VaultShare.Worker.dll storage-check` dan investigasi semua
   missing file ID sebelum membuka traffic.
5. Download/decrypt synthetic sample dan cocokkan SHA-256.
6. Verifikasi audit continuity, membership, share expiry, dan job schedule.
7. Rotasi credential yang mungkin terpapar, lalu re-enable service bertahap.

Object versioning dan immutable backup dapat menahan data setelah user purge;
retensinya harus dijelaskan dalam privacy notice dan tidak boleh disebut
penghapusan fisik instan.
