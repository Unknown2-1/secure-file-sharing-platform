# VaultShare V1 Release Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing VaultShare V1 scope pass clean full-stack, security, dependency, performance-smoke, and release checks, then publish the verified commit as GitHub release `v1.0.0` without Claude co-author metadata.

**Architecture:** Preserve the modular monolith and non-root containers. Repair the shared upload-volume ownership boundary with an idempotent one-shot initializer, make Railway API/worker definitions consume the maintained root-context Dockerfiles, and promote existing browser behavior into a release gate. Use pinned security actions and evidence-driven documentation; do not add deferred ZIP, E2EE, multi-region, or cloud-KMS features.

**Tech Stack:** ASP.NET Core/.NET 10, Entity Framework Core, PostgreSQL 18, MinIO/S3, ClamAV, Next.js 16.2.10, React 19.2, TypeScript, Playwright 1.61, Docker Compose, GitHub Actions, Gitleaks, Trivy.

## Global Constraints

- Indonesian remains the default UI language and the existing API contracts remain stable.
- API and worker remain non-root; do not solve permissions by running either service as root or by using mode `777`.
- Production requires `CLAMAV_FAIL_CLOSED=true`, HTTPS URLs, explicit HTTPS CORS origins, private object storage, and non-placeholder secrets.
- The environment-backed 32-byte KEK remains the supported V1 provider; do not claim it is a cloud KMS.
- No forced Next.js downgrade and no unsupported dependency graph may be used to silence npm audit.
- Every behavior/configuration change follows RED–GREEN–REFACTOR with a failing executable check first.
- No release/tag is published until local verification and GitHub Actions are green.
- Remote history is rewritten only if a fresh GitHub API scan finds Claude/Anthropic metadata on `main`; current evidence says no rewrite is required.
- Third-party security actions are pinned to immutable full commit SHAs.

## File Responsibility Map

- `docker-compose.yml`: shared-volume initializer and service dependency ordering.
- `infrastructure/docker/api.Dockerfile`: API runtime user and writable upload-directory metadata.
- `infrastructure/docker/worker.Dockerfile`: worker runtime user and writable upload-directory metadata.
- `infrastructure/scripts/verify-release-config.sh`: executable static/configuration regression checks.
- `infrastructure/scripts/verify-running-stack.sh`: executable health and runtime permission checks.
- `railway.toml`: repository-root API service deployment definition.
- `railway.worker.toml`: repository-root worker service deployment definition.
- `frontend/package.json` and `frontend/package-lock.json`: supported PostCSS override and E2E/performance scripts.
- `frontend/tests/e2e/vaultshare.spec.ts`: four core browser scenarios plus bounded 1 MiB pipeline smoke.
- `.github/workflows/integration-e2e.yml`: clean Compose, runtime verification, core E2E, and performance smoke.
- `.github/workflows/security.yml`: dependency/CodeQL plus full-history secret and filesystem scans.
- `.github/workflows/container-build.yml`: load and scan each built runtime image.
- `STATUS.md`, `TASKS.md`, `CHANGELOG.md`, `SECURITY.md`, `README.md`, and `docs/*.md`: release evidence and accurate operational boundaries.

---

### Task 1: Repair the non-root temporary-upload volume

**Files:**
- Modify: `docker-compose.yml:70-114`
- Modify: `infrastructure/docker/api.Dockerfile:8-14`
- Modify: `infrastructure/docker/worker.Dockerfile:8-13`
- Create: `infrastructure/scripts/verify-running-stack.sh`
- Test: `frontend/tests/e2e/vaultshare.spec.ts:16-72`

**Interfaces:**
- Consumes: named volume `temp-uploads`, runtime identity `uid=100`, `gid=101`, existing API/worker `TEMP_UPLOAD_PATH`.
- Produces: completed Compose service `temp-uploads-init`; executable `verify-running-stack.sh` returning zero only when health and write permissions are correct.

- [ ] **Step 1: Reproduce the failing behavioral test**

Run from the current stack:

```bash
cd frontend
E2E_BASE_URL=http://localhost:8080 npm run test:e2e
```

Expected RED: the upload/share scenario fails at `menunggu pemeriksaan keamanan`; API logs contain `UnauthorizedAccessException` for `/var/lib/vaultshare/uploads/*.upload`.

- [ ] **Step 2: Add the failing runtime verification script**

Create `infrastructure/scripts/verify-running-stack.sh`:

```sh
#!/bin/sh
set -eu

base_url="${VAULTSHARE_BASE_URL:-http://localhost:8080}"

curl -fsS "${base_url}/health/live" >/dev/null
curl -fsS "${base_url}/health/ready" >/dev/null
curl -fsS "${base_url}/" >/dev/null

docker compose exec -T api sh -ec 'test "$(id -u)" = 100; test "$(id -g)" = 101; test -w /var/lib/vaultshare/uploads'
docker compose exec -T worker sh -ec 'test "$(id -u)" = 100; test "$(id -g)" = 101; test -w /var/lib/vaultshare/uploads'
```

Run `chmod +x infrastructure/scripts/verify-running-stack.sh` and then run the script. Expected RED: `test -w` fails for API and worker.

- [ ] **Step 3: Add the one-shot initializer and image directory ownership**

In `docker-compose.yml`, add before `api`:

```yaml
  temp-uploads-init:
    image: alpine:3.23
    command: ["sh", "-ec", "chown -R 100:101 /uploads && chmod 750 /uploads"]
    volumes:
      - temp-uploads:/uploads
    networks: [backend]
```

Add this dependency to both `api.depends_on` and `worker.depends_on`:

```yaml
      temp-uploads-init:
        condition: service_completed_successfully
```

In both runtime Dockerfiles, after creating the user and before `USER vaultshare`, add:

```dockerfile
RUN mkdir -p /var/lib/vaultshare/uploads \
    && chown -R vaultshare:vaultshare /var/lib/vaultshare \
    && chmod 750 /var/lib/vaultshare/uploads
```

- [ ] **Step 4: Rebuild the affected services and verify GREEN**

Run:

```bash
docker compose build api worker
docker compose up -d --force-recreate temp-uploads-init api worker --wait --wait-timeout 300
infrastructure/scripts/verify-running-stack.sh
cd frontend
E2E_BASE_URL=http://localhost:8080 npm run test:e2e
```

Expected GREEN: runtime verification exits zero and all four core E2E scenarios pass.

- [ ] **Step 5: Commit the runtime repair**

```bash
git add docker-compose.yml infrastructure/docker/api.Dockerfile infrastructure/docker/worker.Dockerfile infrastructure/scripts/verify-running-stack.sh
git commit -m "fix: initialize writable upload volume"
```

---

### Task 2: Remove the PostCSS advisory without downgrading Next.js

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`

**Interfaces:**
- Consumes: Next.js `16.2.10`, which declares nested PostCSS `8.4.31`.
- Produces: npm override resolving every PostCSS instance to patched `8.5.19` while keeping Next.js `16.2.10`.

- [ ] **Step 1: Verify the dependency check is RED**

Run:

```bash
cd frontend
npm audit --omit=dev --audit-level=moderate
```

Expected RED: exit 1 with `GHSA-qx2v-qp2m-jg93` and two moderate PostCSS findings.

- [ ] **Step 2: Add the supported package-manager override**

Add this top-level object to `frontend/package.json`:

```json
"overrides": {
  "postcss": "8.5.19"
}
```

Regenerate only through npm:

```bash
cd frontend
npm install --package-lock-only
npm ci
```

- [ ] **Step 3: Verify the override and full frontend are GREEN**

Run:

```bash
npm ls postcss
npm audit --omit=dev --audit-level=moderate
npm run lint
npm run typecheck
npm test -- --run
npm run build
```

Expected: all PostCSS nodes resolve to `8.5.19`; audit exits zero; lint/typecheck/tests/build exit zero.

- [ ] **Step 4: Commit the dependency repair**

```bash
git add frontend/package.json frontend/package-lock.json
git commit -m "fix: override vulnerable transitive postcss"
```

---

### Task 3: Make Railway API and worker configuration self-consistent

**Files:**
- Create: `infrastructure/scripts/verify-release-config.sh`
- Create: `railway.toml`
- Create: `railway.worker.toml`
- Delete: `backend/railway.toml`
- Delete: `backend/Dockerfile`
- Modify: `docs/deployment.md`

**Interfaces:**
- Produces: Railway config paths `/railway.toml` and `/railway.worker.toml`, both using repository-root Docker build context.
- Consumes: existing maintained Dockerfiles and `ProductionConfigurationGuard` required environment variables.

- [ ] **Step 1: Write the failing release-config verifier**

Create `infrastructure/scripts/verify-release-config.sh`:

```sh
#!/bin/sh
set -eu

require_line() {
  file="$1"
  pattern="$2"
  grep -Eq "$pattern" "$file" || {
    echo "Missing required pattern in ${file}: ${pattern}" >&2
    exit 1
  }
}

forbidden_line() {
  file="$1"
  pattern="$2"
  if grep -Eq "$pattern" "$file"; then
    echo "Forbidden pattern in ${file}: ${pattern}" >&2
    exit 1
  fi
}

docker compose config --quiet
require_line railway.toml '^builder = "DOCKERFILE"$'
require_line railway.toml '^dockerfilePath = "infrastructure/docker/api.Dockerfile"$'
require_line railway.worker.toml '^dockerfilePath = "infrastructure/docker/worker.Dockerfile"$'
forbidden_line railway.toml 'CLAMAV_FAIL_CLOSED[[:space:]]*=[[:space:]]*"false"'
forbidden_line railway.worker.toml 'CLAMAV_FAIL_CLOSED[[:space:]]*=[[:space:]]*"false"'
test ! -e backend/railway.toml
test ! -e backend/Dockerfile
```

Make it executable and run it. Expected RED: root Railway files are missing and obsolete backend files still exist.

- [ ] **Step 2: Replace the broken API definition**

Create root `railway.toml`:

```toml
[build]
builder = "DOCKERFILE"
dockerfilePath = "infrastructure/docker/api.Dockerfile"
watchPatterns = ["backend/**", "Directory.*", "global.json", "infrastructure/docker/api.Dockerfile"]

[deploy]
healthcheckPath = "/health/live"
healthcheckTimeout = 300
restartPolicyType = "ON_FAILURE"
restartPolicyMaxRetries = 10
```

Delete `backend/railway.toml` and `backend/Dockerfile` so Railway cannot select an ambiguous root or restore path.

- [ ] **Step 3: Add the worker service definition**

Create `railway.worker.toml`:

```toml
[build]
builder = "DOCKERFILE"
dockerfilePath = "infrastructure/docker/worker.Dockerfile"
watchPatterns = ["backend/**", "Directory.*", "global.json", "infrastructure/docker/worker.Dockerfile"]

[deploy]
restartPolicyType = "ON_FAILURE"
restartPolicyMaxRetries = 10
```

- [ ] **Step 4: Document exact Railway service settings and secrets**

Update `docs/deployment.md` with:

```markdown
## Railway shared-monorepo deployment

Create separate API and worker services from the repository root. Set the API
config path to `/railway.toml` and the worker config path to
`/railway.worker.toml`; do not set the service root directory to `/backend`
because both builds consume root `Directory.*` and `global.json` files.

Inject `ASPNETCORE_ENVIRONMENT=Production`, `SEED_DEMO_DATA=false`,
`CLAMAV_FAIL_CLOSED=true`, the database/object-storage/ClamAV connection
variables, explicit HTTPS `PUBLIC_APP_URL`, `FRONTEND_URL`, and
`CORS_ALLOWED_ORIGINS`, plus independent 32-byte `FILE_ENCRYPTION_KEK` and
`PRIVACY_IP_HASH_KEY` secrets. The API-only service is incomplete: the worker
must run against the same PostgreSQL, object storage, ClamAV, and KEK.
```

Also state that Railway variables are dashboard/secret-manager values and are intentionally absent from config-as-code.

- [ ] **Step 5: Verify and commit deployment configuration**

Run:

```bash
infrastructure/scripts/verify-release-config.sh
docker build -f infrastructure/docker/api.Dockerfile -t vaultshare/api:release-check .
docker build -f infrastructure/docker/worker.Dockerfile -t vaultshare/worker:release-check .
```

Expected GREEN: config script and both builds exit zero.

Commit:

```bash
git add -A railway.toml railway.worker.toml backend/railway.toml backend/Dockerfile infrastructure/scripts/verify-release-config.sh docs/deployment.md
git commit -m "fix: align railway deployment with production guards"
```

---

### Task 4: Add a bounded encrypted-pipeline performance smoke

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/tests/e2e/vaultshare.spec.ts`
- Modify: `.github/workflows/integration-e2e.yml`

**Interfaces:**
- Produces: npm scripts `test:e2e` for the four core scenarios and `test:performance` for one 1 MiB pipeline scenario.
- Consumes: existing `login`, `firstWorkspaceId`, and `waitUntilAvailable` Playwright helpers.

- [ ] **Step 1: Add the performance scenario and verify it fails on the broken baseline if Task 1 is reverted**

Append before helper functions in `vaultshare.spec.ts`:

```ts
test("@performance 1 MiB encrypted pipeline completes within 120 seconds", async ({ page, browser }) => {
  test.setTimeout(130_000);
  await login(page, "owner@example.com", demoPassword);
  const workspaceId = await firstWorkspaceId(page);
  const filename = `performance-${Date.now()}.bin`;
  const fixture = Buffer.alloc(1024 * 1024, 0x5a);
  const startedAt = Date.now();

  await page.goto(`/upload?workspace=${workspaceId}`);
  await page.locator('input[type="file"]').setInputFiles({
    name: filename,
    mimeType: "application/octet-stream",
    buffer: fixture,
  });
  await page.getByRole("button", { name: "Unggah" }).click();
  await expect(page.getByText(/menunggu pemeriksaan keamanan/)).toBeVisible();
  await waitUntilAvailable(page, workspaceId, filename);

  await page.goto(`/shares/new?workspace=${workspaceId}`);
  await page.getByText(filename, { exact: true }).click();
  await page.getByLabel("2. Nama share").fill(`Performance ${Date.now()}`);
  await page.getByLabel("Password opsional").fill("PerformancePass123!");
  await page.getByRole("button", { name: "Buat share" }).click();
  const shareUrl = await page.getByLabel("Link share").inputValue();

  const recipient = await browser.newContext();
  const publicPage = await recipient.newPage();
  await publicPage.goto(shareUrl);
  await publicPage.getByLabel("Password share").fill("PerformancePass123!");
  await publicPage.getByRole("button", { name: "Lanjutkan" }).click();
  const downloadUrl = expectString(await publicPage.getByRole("link", { name: "Unduh" }).getAttribute("href"));
  const download = await recipient.request.get(downloadUrl);
  expect(download.status()).toBe(200);
  expect(await download.body()).toEqual(fixture);
  await recipient.close();

  expect(Date.now() - startedAt).toBeLessThan(120_000);
});
```

Before the Task 1 fix this test fails at upload; after Task 1 it becomes the GREEN regression.

- [ ] **Step 2: Split core and performance scripts**

Change the scripts to:

```json
"test:e2e": "playwright test --grep-invert @performance",
"test:performance": "playwright test --grep @performance",
"test:e2e:headed": "playwright test --grep-invert @performance --headed"
```

- [ ] **Step 3: Make full-stack CI run from clean volumes on `main`**

Update `.github/workflows/integration-e2e.yml` triggers:

```yaml
on:
  push:
    branches: [main]
    paths:
      - "backend/**"
      - "frontend/**"
      - "infrastructure/**"
      - "docker-compose.yml"
      - "Directory.*"
      - "global.json"
      - ".github/workflows/integration-e2e.yml"
  pull_request:
  workflow_dispatch:
```

Add before `docker compose up`:

```yaml
      - name: Ensure clean Compose state
        run: docker compose down -v --remove-orphans
```

Add after stack startup and after core browser scenarios:

```yaml
      - name: Verify running stack and upload volume
        run: infrastructure/scripts/verify-running-stack.sh
      - name: Run bounded performance smoke
        working-directory: frontend
        env:
          E2E_BASE_URL: http://localhost:8080
        run: npm run test:performance
```

- [ ] **Step 4: Verify both suites and commit**

Run:

```bash
cd frontend
E2E_BASE_URL=http://localhost:8080 npm run test:e2e
E2E_BASE_URL=http://localhost:8080 npm run test:performance
```

Expected: core reports 4 passed; performance reports 1 passed under 120 seconds.

Commit:

```bash
git add frontend/package.json frontend/tests/e2e/vaultshare.spec.ts .github/workflows/integration-e2e.yml
git commit -m "test: gate release on full-stack performance smoke"
```

---

### Task 5: Add immutable secret, filesystem, and container scans

**Files:**
- Modify: `.github/workflows/security.yml`
- Modify: `.github/workflows/container-build.yml`

**Interfaces:**
- Consumes: Gitleaks Action commit `ff98106e4c7b2bc287b24eaf42907196329070c7` (`v2.3.9`), Trivy Action commit `ed142fd0673e97e23eac54620cfb913e5ce36c25` (`v0.36.0`).
- Produces: required jobs `secret-scan`, `filesystem-scan`, and image scan for API/worker/frontend matrix entries.

- [ ] **Step 1: Establish RED by listing absent scan jobs**

Run:

```bash
rg -n "gitleaks-action|trivy-action|secret-scan|filesystem-scan" .github/workflows
```

Expected RED: no matches.

- [ ] **Step 2: Add a full-history Gitleaks job**

Add to `security.yml`:

```yaml
  secret-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: gitleaks/gitleaks-action@ff98106e4c7b2bc287b24eaf42907196329070c7 # v2.3.9
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          GITLEAKS_ENABLE_COMMENTS: "false"
```

- [ ] **Step 3: Add a filesystem vulnerability/misconfiguration job**

Add to `security.yml`:

```yaml
  filesystem-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: aquasecurity/trivy-action@ed142fd0673e97e23eac54620cfb913e5ce36c25 # v0.36.0
        with:
          scan-type: fs
          scan-ref: .
          scanners: vuln,secret,misconfig
          severity: HIGH,CRITICAL
          ignore-unfixed: true
          exit-code: 1
```

- [ ] **Step 4: Load and scan each built container image**

In `container-build.yml`, add `load: true` to `docker/build-push-action`, then add:

```yaml
      - name: Scan ${{ matrix.name }} image
        uses: aquasecurity/trivy-action@ed142fd0673e97e23eac54620cfb913e5ce36c25 # v0.36.0
        with:
          image-ref: vaultshare/${{ matrix.name }}:ci
          severity: HIGH,CRITICAL
          ignore-unfixed: true
          exit-code: 1
```

- [ ] **Step 5: Validate workflow structure and commit**

Run:

```bash
rg -n "ff98106e4c7b2bc287b24eaf42907196329070c7|ed142fd0673e97e23eac54620cfb913e5ce36c25" .github/workflows
git diff --check
```

Expected: immutable SHAs appear, no mutable Gitleaks/Trivy tag is used, diff check exits zero.

Commit:

```bash
git add .github/workflows/security.yml .github/workflows/container-build.yml
git commit -m "ci: add secret filesystem and image scans"
```

---

### Task 6: Reconcile release documentation and security contact

**Files:**
- Modify: `SECURITY.md`
- Modify: `frontend/app/security/page.tsx`
- Create: `frontend/tests/security-page.test.tsx`
- Modify: `README.md`
- Modify: `STATUS.md`
- Modify: `TASKS.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/testing.md`
- Modify: `docs/production-hardening.md`
- Modify: `docs/key-management.md`
- Modify: `docs/deployment.md`

**Interfaces:**
- Produces: consistent public contact `dnshadelio@gmail.com` and evidence dated `2026-07-18`.
- Consumes: actual command counts from Tasks 1–5; no unverified claim may be added.

- [ ] **Step 1: Add a failing frontend assertion for the real security contact**

Create `frontend/tests/security-page.test.tsx`:

```ts
import { render, screen } from "@testing-library/react";
import SecurityInfoPage from "@/app/security/page";

test("security page publishes the repository owner's real contact", () => {
  render(<SecurityInfoPage />);

  expect(screen.getByText(/dnshadelio@gmail\.com/)).toBeInTheDocument();
  expect(screen.queryByText(/security@example\.com/)).not.toBeInTheDocument();
});
```

Run the focused test. Expected RED: current page still renders `security@example.com`.

- [ ] **Step 2: Replace placeholder contact everywhere**

Replace `security@example.com` with `dnshadelio@gmail.com` in `SECURITY.md` and `frontend/app/security/page.tsx`. Remove all placeholder wording. Run:

```bash
rg -n "security@example.com|placeholder" SECURITY.md frontend/app/security/page.tsx
```

Expected GREEN: no matches.

- [ ] **Step 3: Update V1 scope and evidence documents**

Make the documents state exactly:

- Compose is installed and verified from clean volumes.
- Backend has 49 tests; all pass with zero skips.
- Frontend has 34 unit/component tests after the focused security-page test.
- Core browser E2E is 4/4 and performance smoke is 1/1.
- NuGet and npm audit pass; PostCSS is overridden to patched `8.5.19`.
- Environment-backed KEK is V1 and not cloud KMS; cloud KMS is a future provider option.
- ZIP remains an explicitly documented non-goal, not an unfinished V1 requirement.
- Full-stack, dependency, CodeQL, secret, filesystem, and container workflows are release gates.
- `TASKS.md` Phase 1, Phase 11, and Phase 12 verification boxes are checked only after Task 7 succeeds.
- `CHANGELOG.md` moves the V1 content under `[1.0.0] - 2026-07-18` only immediately before release.

- [ ] **Step 4: Verify prose consistency and commit**

Run:

```bash
rg -n "Docker.*tidak|belum.*E2E|security@example.com|production KMS provider.*belum|1\.0\.0.*Unreleased" README.md STATUS.md TASKS.md CHANGELOG.md SECURITY.md docs frontend/app/security/page.tsx
git diff --check
```

Expected: no stale blocker/placeholder claims remain; ZIP and cloud KMS appear only as explicit non-goals/boundaries.

Commit:

```bash
git add SECURITY.md frontend/app/security/page.tsx frontend/tests README.md STATUS.md TASKS.md CHANGELOG.md docs
git commit -m "docs: finalize v1 release evidence and security contact"
```

---

### Task 7: Run the complete release verification from a clean state

**Files:**
- Modify only if a verification failure reveals a root cause; follow a new RED–GREEN cycle for each failure.

**Interfaces:**
- Produces: fresh local evidence for the release commit.

- [ ] **Step 1: Verify repository and backend**

Run:

```bash
git diff --check
./.dotnet/dotnet restore backend/VaultShare.sln --locked-mode
./.dotnet/dotnet format backend/VaultShare.sln --verify-no-changes --no-restore
./.dotnet/dotnet build backend/VaultShare.sln -c Release --no-restore
./.dotnet/dotnet test backend/VaultShare.sln -c Release --no-build -m:1
./.dotnet/dotnet list backend/VaultShare.sln package --vulnerable --include-transitive
```

Expected: clean diff; zero warnings/errors; all backend tests pass; no vulnerable NuGet packages.

- [ ] **Step 2: Verify frontend**

Run:

```bash
cd frontend
npm ci
npm audit --omit=dev --audit-level=moderate
npm run lint
npm run typecheck
npm test -- --run
npm run build
```

Expected: every command exits zero with no audit findings at moderate or higher.

- [ ] **Step 3: Recreate the entire Compose stack and verify runtime**

Run the clean stack under an isolated Compose project so the existing local
PostgreSQL/MinIO volumes are preserved. Stop the default project without
removing its volumes, create the isolated project, and restore the default
project after cleanup:

```bash
docker compose stop
COMPOSE_PROJECT_NAME=vaultshare-release docker compose up -d --build --wait --wait-timeout 300
COMPOSE_PROJECT_NAME=vaultshare-release infrastructure/scripts/verify-release-config.sh
COMPOSE_PROJECT_NAME=vaultshare-release infrastructure/scripts/verify-running-stack.sh
cd frontend
E2E_BASE_URL=http://localhost:8080 npm run test:e2e
E2E_BASE_URL=http://localhost:8080 npm run test:performance
cd ..
COMPOSE_PROJECT_NAME=vaultshare-release docker compose down -v --remove-orphans
docker compose up -d --wait --wait-timeout 300
```

Expected: all required services healthy, four core tests pass, one performance test passes.
If any test fails, tear down only `vaultshare-release` and restore the default
project before investigating; never remove the default project's volumes.

- [ ] **Step 4: Inspect runtime errors and finalize evidence**

Run:

```bash
docker compose logs --no-color api worker frontend proxy | rg -n "Unhandled exception|UnauthorizedAccessException|permission denied|\[FTL\]"
git status --short --branch
```

Expected: no error-pattern matches and a clean working tree. If status docs need count corrections, amend only the documentation commit and rerun the affected checks.

---

### Task 8: Clean obsolete metadata, publish main, and create `v1.0.0`

**Files:**
- No source changes expected after Task 7.
- External state: local Git refs, GitHub `main`, Dependabot pull requests, Actions, tag, and release.

**Interfaces:**
- Produces: GitHub default branch with owner-only commit identity and release `v1.0.0`.

- [ ] **Step 1: Verify public history before any rewrite decision**

Run:

```bash
git log main --format='%H%n%an%n%ae%n%B' | rg -ni "claude|anthropic|noreply@anthropic\.com"
gh api 'repos/Unknown2-1/secure-file-sharing-platform/commits?sha=main&per_page=100' --paginate --jq '.[].commit.message' | rg -ni "claude|anthropic|noreply@anthropic\.com"
gh api repos/Unknown2-1/secure-file-sharing-platform/contributors --paginate --jq '.[].login'
```

Expected: both metadata searches have no matches; contributor output is only `Unknown2-1`. Therefore do not rewrite `main`.

- [ ] **Step 2: Remove the obsolete local rewrite ref safely**

Record the ref target in a temporary audit note, then delete only the exact ref:

```bash
git rev-parse refs/original/refs/remotes/origin/main > /tmp/vaultshare-preexisting-original-ref.txt
git update-ref -d refs/original/refs/remotes/origin/main
git show-ref | rg 'refs/original' || true
```

Expected: no `refs/original` remains. Do not delete normal branches, tags, or reflogs.

- [ ] **Step 3: Push the verified commit normally**

Run:

From the isolated release worktree, run:

```bash
git push origin HEAD:main
git branch --set-upstream-to=origin/main
```

Expected: fast-forward push succeeds and the release branch tracks
`origin/main`. If and only if GitHub rejects because remote changed, fetch and
inspect; do not force-push over unseen work.

- [ ] **Step 4: Close obsolete Dependabot PRs and delete their branches**

For each open Dependabot PR returned by `gh pr list --author app/dependabot --state open`, run:

```bash
gh pr close PR_NUMBER --repo Unknown2-1/secure-file-sharing-platform --delete-branch --comment "Superseded by the verified v1.0.0 release baseline; Dependabot may recreate applicable updates against current main."
```

Expected: no stale Dependabot PR remains open. Do not close non-Dependabot PRs.

- [ ] **Step 5: Wait for required GitHub Actions on the release commit**

Run:

```bash
gh run list --repo Unknown2-1/secure-file-sharing-platform --commit "$(git rev-parse HEAD)" --json databaseId,workflowName,status,conclusion,url
gh run watch RUN_ID --repo Unknown2-1/secure-file-sharing-platform --exit-status
```

Watch Backend CI, Frontend CI when triggered, Container Build, Security Checks, and Integration and E2E. Expected: every triggered required workflow concludes `success`. Investigate and fix failures before continuing.

- [ ] **Step 6: Create and publish the release**

Run:

```bash
git tag -a v1.0.0 -m "VaultShare v1.0.0"
git push origin v1.0.0
gh release create v1.0.0 --repo Unknown2-1/secure-file-sharing-platform --title "VaultShare v1.0.0" --generate-notes --verify-tag
```

Expected: tag and GitHub Release point to the verified main commit.

- [ ] **Step 7: Final remote verification**

Run:

```bash
gh release view v1.0.0 --repo Unknown2-1/secure-file-sharing-platform --json tagName,isDraft,isPrerelease,url,targetCommitish
gh api repos/Unknown2-1/secure-file-sharing-platform/contributors --paginate --jq '.[] | [.login, .contributions] | @tsv'
git status --short --branch
```

Expected: release is neither draft nor prerelease, contributor identity is only `Unknown2-1`, and local `main` is clean and synchronized with `origin/main`.

After leaving/removing the release worktree, fast-forward the original checkout
without rewriting it:

```bash
git -C /home/danish/Documents/secure-file-sharing-platform pull --ff-only origin main
```
