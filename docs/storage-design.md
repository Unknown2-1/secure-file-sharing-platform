# Storage Design

The S3-compatible bucket is private. Object keys use
`encrypted/{workspace-id}/{random-uuid}` and never filenames. Objects contain
only ciphertext with `application/octet-stream`; sensitive encryption metadata
is stored in PostgreSQL and secrets are excluded from object metadata. User
downloads always pass API authorization and streaming decryption.

