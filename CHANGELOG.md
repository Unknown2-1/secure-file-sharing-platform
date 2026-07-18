# Changelog

Semua perubahan penting mengikuti Keep a Changelog dan Semantic Versioning.

## [Unreleased]

Tidak ada perubahan yang belum dirilis.

## [1.0.0] - 2026-07-18

### Added

- Modular ASP.NET Core identity/workspace API with TOTP, session revocation,
  invitations, backend role policies, and workspace security settings.
- Resumable offset upload, fail-closed ClamAV processing, chunked AES-256-GCM
  envelope encryption, private MinIO adapter, and KEK rewrap command.
- Public/password/one-time/max-download shares, internal grants, streaming
  decryption, safe previews, soft-delete/restore/purge, and crypto-shredding.
- Audit export, notifications/email jobs, retention worker, missing-object
  consistency command, encrypted demo seeder, and dashboard metrics.
- Next.js Indonesian UI, component tests, Playwright scenarios, Docker Compose,
  CI/container/CodeQL workflows, threat model, and operations documentation.
- Clean full-stack release gate, bounded 1 MiB encrypted-pipeline performance
  smoke, full-history secret scan, filesystem scan, and runtime image scans.

### Changed

- Phase 10 complete: Full localization (Indonesian/English), async states
  (loading/empty/error), responsive UI for dashboard/files/shares, accessibility
  improvements (skip link, ARIA, semantic HTML), and comprehensive test coverage.
- Security: Upgraded Next.js to 16.2.10 patching high-severity vulnerabilities
  (GHSA-q4gf-8mx6-v5v3, GHSA-8h8q-6873-q5fj, GHSA-3g8h-86w9-wvmq,
  GHSA-ffhc-5mcf-pf4q, GHSA-vfv6-92ff-j949, GHSA-gx5p-jg67-6x7h,
  GHSA-mg66-mrh9-m8jx, GHSA-h64f-5h5j-jqjh, GHSA-c4j6-fc7j-m34r,
  GHSA-492v-c6pp-mqqv, GHSA-wfc6-r584-vfw7, GHSA-267c-6grr-h53f,
  GHSA-36qx-fr4f-26g5).
- Security: Forced the transitive PostCSS graph to patched 8.5.19 without a
  Next.js downgrade and published the real private-report contact.
- Deployment: Aligned Railway API and worker services with the maintained
  repository-root Dockerfiles and production fail-closed requirements.

### Fixed

- Screenshot portfolio: 8 PNG screenshots added showing landing page,
  dashboard, files, shares, settings (workspace/security), notifications,
  and upload UI states.
- Non-root API/worker upload volume initialization and PostgreSQL retry-aware
  download reservation transactions.
