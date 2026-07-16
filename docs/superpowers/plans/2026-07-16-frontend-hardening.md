# Frontend Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an enforceable `id/en` localization boundary and accessible loading, empty, error, and destructive-confirmation behavior to the existing VaultShare frontend.

**Architecture:** A dependency-free typed message catalog provides stable UI messages and locale fallback. Shared semantic feedback components render consistent live regions, while feature components retain their current fetch and mutation responsibilities.

**Tech Stack:** Next.js 16, React 19, strict TypeScript, Tailwind CSS 4, Vitest, Testing Library.

## Global Constraints

- Indonesian remains the default UI language.
- Public share pages load no external font, script, analytics, or asset.
- Backend authorization remains authoritative.
- No authentication token is stored in local storage or exposed in UI errors.
- All behavior changes follow RED–GREEN–REFACTOR.
- Git commits are omitted because `.git` is a read-only invalid mount in this environment.

---

### Task 1: Typed message catalog

**Files:**
- Create: `frontend/lib/i18n/messages.ts`
- Test: `frontend/tests/i18n.test.ts`

**Interfaces:**
- Produces: `Locale`, `MessageKey`, `messages`, `translate(locale, key)`.

- [ ] **Step 1: Write a failing test** asserting Indonesian default messages, English parity, unknown-locale fallback, and identical catalog keys.
- [ ] **Step 2: Run `npm test -- i18n.test.ts`** and confirm failure because `@/lib/i18n/messages` does not exist.
- [ ] **Step 3: Implement immutable `id` and `en` catalogs** plus a fallback-only `translate` function without runtime network access.
- [ ] **Step 4: Run `npm test -- i18n.test.ts`** and confirm all catalog tests pass.

### Task 2: Shared semantic feedback states

**Files:**
- Create: `frontend/components/ui/async-state.tsx`
- Test: `frontend/tests/async-state.test.tsx`
- Modify: `frontend/app/globals.css`

**Interfaces:**
- Produces: `LoadingState`, `EmptyState`, `ErrorState` React components.
- Consumes: message catalog keys for default copy.

- [ ] **Step 1: Write failing component tests** for loading `role=status`, error `role=alert`, empty-state action, and retry activation.
- [ ] **Step 2: Run `npm test -- async-state.test.tsx`** and confirm the missing-module failure.
- [ ] **Step 3: Implement the three semantic components** with visible headings, descriptions, and a 44 px retry/action target.
- [ ] **Step 4: Run the focused tests** and confirm they pass.

### Task 3: Async form feedback

**Files:**
- Modify: `frontend/components/account-code-form.tsx`
- Modify: `frontend/components/accept-invitation-form.tsx`
- Modify: `frontend/components/two-factor-form.tsx`
- Test: `frontend/tests/account-code-form.test.tsx`

**Interfaces:**
- Consumes: `translate("id", key)` and existing `getCsrfToken()`.
- Produces: disabled submit controls and status text during active requests.

- [ ] **Step 1: Write a failing test** that holds a fetch promise open and asserts the recovery form button is disabled with `Memproses…`.
- [ ] **Step 2: Run the focused test** and confirm it fails on the current enabled `Kirim` button.
- [ ] **Step 3: Add guarded loading state and safe `try/catch/finally` handling** to the three forms.
- [ ] **Step 4: Re-run focused and existing frontend tests** and confirm they pass.

### Task 4: Lists and destructive confirmation

**Files:**
- Modify: `frontend/components/dashboard-shell.tsx`
- Modify: `frontend/components/file-list.tsx`
- Modify: `frontend/components/share-list.tsx`
- Modify: `frontend/components/notification-center.tsx`
- Modify: `frontend/components/workspace-members.tsx`
- Test: `frontend/tests/workspace-members.test.tsx`

**Interfaces:**
- Consumes: shared state components.
- Produces: explicit loading/empty/error rendering and an in-page member-removal confirmation.

- [ ] **Step 1: Write a failing member test** asserting that selecting `Hapus` reveals a named confirmation region and that `Batal` performs no DELETE request.
- [ ] **Step 2: Run the focused test** and confirm failure because the component calls `window.confirm`.
- [ ] **Step 3: Replace native confirmation with semantic in-page confirmation** and add loading/error/empty state distinctions to priority lists.
- [ ] **Step 4: Run all frontend unit tests** and confirm no regressions.

### Task 5: Verification and project records

**Files:**
- Modify: `TASKS.md`
- Modify: `STATUS.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/testing.md`

**Interfaces:**
- Consumes: final command evidence.
- Produces: an honest project-state record without claiming browser/full-stack verification.

- [ ] **Step 1: Run `npm run lint`, `npm run typecheck`, and `npm test`.**
- [ ] **Step 2: Run `npm run build` and `npm run test:e2e -- --list`.**
- [ ] **Step 3: Run targeted source scans for external assets, inaccessible native confirmation, and message catalog parity.**
- [ ] **Step 4: Update project records only for checks proven by command output.**
