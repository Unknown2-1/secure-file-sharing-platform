# ADR 0001: Platform versions

- Status: Accepted
- Date: 2026-07-16

## Decision

VaultShare targets .NET 10 LTS with EF Core/Npgsql 10, PostgreSQL 18, Next.js
16.2, React 19, and Node.js 24 LTS for containers and CI. The host's Node.js 26
is a Current release and may be used for local tooling only when lockfile tests
remain green.

## Rationale

The backend uses the current stable LTS instead of a preview. Next.js 16.3 was
still preview-only during inspection, so 16.2 is the stable production target.
Node.js production images use an LTS release rather than the host's Current
channel.

## Consequences

All packages are centrally pinned. Upgrades require CI, integration, security,
and migration verification before this ADR is superseded.

