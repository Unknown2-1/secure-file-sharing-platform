# ADR 0003: Offset-based resumable upload protocol

- Status: Accepted
- Date: 2026-07-16

## Context

VaultShare needs chunked, cancellable, resumable uploads without buffering an entire file. The preferred tus ecosystem could not be evaluated or restored in the current environment, and adding an unverified protocol dependency would make the build less reproducible.

## Decision

Version 1 starts with a small, documented offset protocol under `/api/v1/uploads`:

- create a session with an idempotency key and declared immutable metadata;
- inspect the authoritative server offset with `GET /api/v1/uploads/{id}`;
- append `application/offset+octet-stream` chunks with `Upload-Offset`;
- accept only the exact current offset and cap chunks at 8 MiB;
- finalize idempotently only when the stored byte count equals the declared size.

Temporary object names are server-generated UUIDs. Workspace permission, quota, size, filename, extension, and session ownership are enforced by the API. File content remains unavailable until the later scan and encryption pipeline completes.

## Consequences

The protocol is intentionally not advertised as tus-compatible. A tus adapter can be added later without changing the domain state machine. Clients must query and send exact offsets; overlapping or sparse writes are rejected. PostgreSQL concurrency tokens and exclusive temporary-file access protect concurrent chunk writes.
