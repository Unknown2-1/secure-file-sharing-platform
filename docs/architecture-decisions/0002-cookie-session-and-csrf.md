# ADR 0002: Cookie sessions, CSRF, and database revocation

- Status: Accepted
- Date: 2026-07-16

## Decision

VaultShare uses ASP.NET Core Identity application cookies. Production cookies
use the `__Host-` prefix, Secure, HttpOnly, SameSite=Lax, path `/`, and no Domain.
Local HTTP uses a non-prefixed cookie without Secure. Every unsafe controller
action is protected by ASP.NET Core antiforgery validation; the SPA reads only
the separate request token and submits it in `X-CSRF-TOKEN`.

Each login also creates a database session ID embedded as a claim. Cookie
validation checks Identity's security stamp first, then rejects revoked or
expired database sessions. Password reset revokes all active sessions.

## Consequences

Protected requests perform a bounded indexed session lookup. This enables active
session listing and immediate server-side revocation without storing auth tokens
in localStorage. Distributed deployments require shared Data Protection keys and
the same PostgreSQL session store.
