# Key Rotation

Rotation enumerates encryption metadata in bounded batches, unwraps each DEK
with the old provider, rewrap it with the new KEK, and update metadata using
optimistic concurrency. Dry-run reports scope only. Rollback keeps the prior KEK
available until every batch and sampled download is verified; every rewrap emits
an audit event without key material (audit persistence is completed in Phase 9).

```bash
# Configure FILE_ENCRYPTION_KEK_NEXT and FILE_ENCRYPTION_KEY_ID_NEXT in the
# process environment. Do not place the KEK on the command line.
dotnet run --project backend/src/VaultShare.Worker -- rewrap-keys --dry-run --batch-size 100
dotnet run --project backend/src/VaultShare.Worker -- rewrap-keys --batch-size 100
```

Run batches while the current KEK remains configured. After all records use the
new identifier, restart API and worker with the new KEK as the current key and
retain the old KEK in the secret manager for the documented rollback window.
Sample downloads must be authenticated before retiring it. A failed database
save leaves that batch unchanged; a rotation never rewrites ciphertext objects.
