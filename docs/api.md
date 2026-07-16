# API

REST API menggunakan prefix `/api/v1`. Authentication memakai cookie HttpOnly;
request mutasi user terautentikasi harus mengirim `X-CSRF-TOKEN` yang diperoleh
dari `GET /api/v1/auth/csrf`. Operasi create/finalize/revoke/delete menggunakan
`Idempotency-Key` ASCII 8–128 karakter. Error memakai RFC Problem Details,
stable `code`, dan `correlationId`; response header juga mengirim
`X-Correlation-ID`.

| Group | Endpoint penting |
|---|---|
| Auth | `/auth/register`, `/login`, `/logout`, `/verify-email`, `/forgot-password`, `/reset-password`, `/two-factor/*`, `/me` |
| Sessions | `/sessions`, `/sessions/{id}` |
| Workspace | `/workspaces`, `/{id}/members`, `/{id}/invitations`, `/{id}/settings` |
| Upload | `/uploads`, `/uploads/{id}` (PATCH offset), `/finalize` |
| Files | `/files`, `/files/{id}`, delete/restore/purge, `/internal-grants` |
| Share | `/shares`, `/shares/{id}/revoke`, `/public/shares/access` |
| Download | `/downloads/{fileId}`, `/internal-files/{fileId}/download`, `/previews/{fileId}` |
| Operations | `/dashboard`, `/notifications`, `/audit-events`, `/audit-events/export` |

List file menggunakan `page`, `pageSize` (maksimum 100), `search`, dan `status`.
Swagger hanya tersedia pada Development. DTO tidak pernah memuat object key,
token/password hash, wrapped DEK, internal path, cookie, atau Identity security
stamp. Endpoint public share menerima secret melalui JSON body; public page URL
tidak diteruskan ke analytics dan proxy tidak mencatat `/s/`.

Upload PATCH menggunakan `Content-Type: application/offset+octet-stream`,
`Upload-Offset`, serta chunk maksimum 8 MiB. Server mengembalikan offset baru di
header. Protocol ini didokumentasikan di ADR 0003 dan sengaja tidak diklaim
sebagai tus.
