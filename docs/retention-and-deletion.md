# Retention and Deletion

Soft-deleted files may be restored during a configurable grace period. Purge is
idempotent: revoke related shares, delete ciphertext, remove wrapped DEK, clean
temporary data, mark purged, and emit a safe audit event. Removing the wrapped
DEK provides crypto-shredding semantics, while backups may retain data under a
separate documented policy.

