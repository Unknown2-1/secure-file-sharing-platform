# ADR 0004 — Database-state background worker

Status: Accepted

## Context

VaultShare membutuhkan processing/cleanup yang idempotent. Hangfire PostgreSQL
storage menambah dashboard dan schema operasional, tetapi upload/file domain
tetap perlu state machine dan concurrency token sendiri.

## Decision

V1 memakai .NET `BackgroundService` terpisah. `FileUpload.Status` dan optimistic
concurrency menjadi durable queue: worker atomically mengubah `Uploaded` menjadi
`Processing`, menjalankan scan/encryption/storage, lalu `Completed`/`Failed`.
Scheduled maintenance dan notification memakai record database yang idempotent,
bounded batch, retry loop, cancellation, dan safe structured logging.

## Consequences

Tidak ada public job dashboard atau dependency Hangfire. Monitoring berasal dari
status database/log/health. Multi-worker processing aman melalui concurrency
token, tetapi schedule canggih dan distributed backoff harus dikembangkan lebih
lanjut bila skala meningkat. Stale `Processing` direkonsiliasi setelah timeout.
