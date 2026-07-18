# VaultShare V1 Release Readiness Design

## Objective

Prepare VaultShare for a defensible `v1.0.0` release by fixing the verified
full-stack upload failure, making production deployment configuration
self-consistent, expanding release-gating automation, reconciling project
documentation with fresh evidence, and removing Claude co-author trailers from
the published Git history.

Release readiness means all repository checks and the complete browser suite
pass from a clean environment. It does not mean that every possible future
feature is included.

## Scope

The release includes:

- A writable shared temporary-upload volume for the non-root API and worker.
- A regression gate that exercises registration, upload, malware scanning,
  encryption, password-protected sharing, download limits, revocation, role
  denial, and the mobile dashboard against a clean Compose stack.
- Production deployment definitions whose environment requirements match the
  application's production guards and architecture.
- Dependency, secret, container, static-analysis, and bounded performance
  checks suitable for release gating.
- Removal of the known PostCSS advisory when it can be done without a framework
  downgrade or an unsupported dependency graph.
- A real security contact using the repository owner's existing public commit
  address, `dnshadelio@gmail.com`.
- Updated status, task, changelog, testing, deployment, and security documents.
- Verification that `main` contains no `Co-Authored-By: Claude Opus 4.8`
  trailers, plus cleanup of obsolete local rewrite references and pull-request
  branches that still point at superseded history.
- A signed-off `v1.0.0` tag and GitHub Release after required checks are green.

## Explicit Non-Goals

- ZIP bundle streaming remains outside V1 because per-entry authenticated
  streaming and abuse controls require a separate design.
- End-to-end encryption, zero-knowledge storage, native mobile applications,
  multi-region operation, and Kubernetes remain outside V1.
- A cloud-vendor KMS integration is not required for V1. Production continues
  to accept a 32-byte KEK injected by the deployment secret manager, with the
  existing rewrap workflow for rotation. The documentation must state this
  boundary without calling the environment-backed provider a cloud KMS.
- The work will not weaken fail-closed malware scanning, production startup
  guards, non-root containers, private object storage, or encrypted-at-rest
  guarantees to make deployment easier.

## Runtime Architecture

The API and worker continue to run as the existing non-root `vaultshare` user.
A one-shot Compose initialization service will mount the shared
`temp-uploads` volume and set ownership to the numeric API/worker UID and GID
before either service starts. Both long-running services will depend on the
successful completion of this initializer.

The API and worker images will also create `/var/lib/vaultshare/uploads` with
the same ownership during image construction. This makes image behavior
correct outside Compose and gives newly populated volumes safe metadata. The
initializer remains necessary for existing volumes whose ownership was
previously created as `root:root`.

The browser suite is the behavioral regression test for this boundary. Its
existing upload scenario already fails for the verified bug, so the first green
run after the configuration change must demonstrate that a real temporary file
can pass through API, worker, ClamAV, encryption, MinIO, and download.

## Production Deployment

Railway configuration will use an unambiguous repository-root build context and
the maintained API Dockerfile. It will not hard-code
`CLAMAV_FAIL_CLOSED=false`; production configuration must require `true`.
Required database, object-storage, ClamAV, public URL, CORS, encryption, and
privacy-hash values will be documented as injected secrets or service
variables.

The worker will have a separate deployment definition using the worker
Dockerfile and the same database, object storage, scanning, encryption, and
temporary-storage requirements as the API. The deployment documentation will
explain that an API-only deployment is incomplete because file processing is
performed by the worker.

Deployment configuration will be validated locally through image builds,
production-guard tests, Compose configuration, service health checks, and the
full browser suite. No live Railway or Vercel deployment is required by this
scope; the requested external mutation is publishing the repository and
release to GitHub.

## CI, Security, and Performance Gates

The full-stack workflow will run for relevant pushes to `main`, pull requests,
and manual dispatches. It will always create ephemeral keys, start from clean
volumes, wait for service health, run Chromium scenarios, preserve failure
artifacts, and tear down its volumes.

The security workflow will retain NuGet/npm audit and CodeQL, and add:

- Secret scanning of the repository and reachable history.
- Container/filesystem vulnerability scanning with a release-blocking threshold
  for high and critical findings.
- A production Compose configuration validation step.

A bounded performance smoke test will upload and download a generated fixture
against the local stack with explicit size and duration limits. It is a
regression signal, not a capacity claim. Large-scale benchmarks remain an
operator-run activity documented separately.

The PostCSS advisory will be resolved only with a supported patched dependency
or a verified package-manager override. The lockfile, framework build, unit
tests, and E2E suite must all pass after the change. A forced Next.js downgrade
is prohibited.

## Git History and GitHub Publication

Direct inspection of local `main`, `origin/main`, the GitHub commits API, and
the GitHub contributors API confirms that the earlier rewrite already removed
the four Claude co-author trailers from the published default branch while
preserving Danish as author and committer. The unwanted messages now exist only
under a local `refs/original` rewrite reference and obsolete dependency-update
history.

The local `refs/original` reference will be deleted after its target hash is
recorded outside Git refs. A second history rewrite or force-push is prohibited
unless a fresh GitHub API check finds Claude/Anthropic metadata on the remote
default branch. Avoiding an unnecessary rewrite preserves stable public commit
hashes while still meeting the requested GitHub outcome.

Open Dependabot pull requests based on obsolete history will be closed and
their repository-owned branches deleted where GitHub permits it. Dependabot
remains enabled so applicable updates can be recreated against the current
history. GitHub Actions must complete successfully on the release commit before
the `v1.0.0` tag and GitHub Release are published.

## Testing and Acceptance Criteria

The release is accepted only when fresh evidence confirms all of the following:

1. `dotnet format` reports no changes.
2. The backend Release build succeeds with zero warnings and all 49 or more
   backend tests pass with zero failures and skips.
3. Frontend lint, typecheck, unit tests, and production build succeed.
4. NuGet reports no known vulnerable packages.
5. npm reports no high or critical vulnerabilities; the tracked PostCSS
   moderate advisory must also be removed for this release.
6. All Compose services start from clean volumes, required services report
   healthy, and live/ready endpoints return HTTP 200.
7. All four Chromium E2E scenarios pass, including the complete upload/share
   path.
8. Secret and container scans pass at their configured release thresholds.
9. The bounded performance smoke test passes its documented limit.
10. Local `main`, remote `main`, and the GitHub commits API contain no
    Claude/Anthropic co-author metadata; obsolete `refs/original` is removed.
11. GitHub Actions for the release commit are green.
12. GitHub shows the release owner as the only contributor identity derived
    from reachable commits, and `v1.0.0` is published with release notes.

## Failure Handling and Rollback

No release or tag will be created while any required check is failing. Runtime
failures will preserve Compose logs and Playwright traces. Security scan
failures will be investigated rather than bypassed by lowering thresholds.

Before deleting `refs/original`, its hash will be recorded in a temporary local
audit note that is not committed or pushed. If a fresh remote check unexpectedly
finds Claude metadata, work stops before release so the user can approve the
exact affected refs; no broad force-push will be improvised.
