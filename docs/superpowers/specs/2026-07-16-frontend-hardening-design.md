# Frontend Hardening Design

## Context

VaultShare already exposes the primary v1 workflows in Indonesian. The remaining Phase 10 gap is not a visual redesign; it is consistent localization boundaries, predictable asynchronous feedback, useful empty and failure states, and accessibility behavior that remains usable on small screens.

## Decision

Use a small typed message catalog in `frontend/lib/i18n` with Indonesian as the default and an English catalog that must have the same keys. Components receive stable message keys instead of backend exception text. Locale selection remains server-controlled and defaults to `id`; a user-facing locale switcher is outside this increment because account locale persistence is not yet part of the backend model.

Use shared semantic `LoadingState`, `EmptyState`, and `ErrorState` components. They expose live-region semantics, an optional retry action, and consistent spacing. Existing domain components keep ownership of data fetching and mutations.

Async forms disable their submit control and expose progress text while a request is active. Destructive workspace-member removal uses an accessible in-page confirmation region instead of `window.confirm`. Lists distinguish loading, empty, and error states rather than rendering an ambiguous blank page.

## Accessibility and responsive constraints

- Interactive controls have at least a 44 px target and visible `:focus-visible` treatment.
- Status changes use `role="status"`; failures use `role="alert"`.
- No third-party fonts, scripts, analytics, or assets are introduced.
- Layouts remain mobile-first and do not require horizontal scrolling at 375 px.
- Reduced-motion behavior remains enforced globally.
- Color is accompanied by text, never used as the only status indicator.

## Testing

Vitest verifies catalog parity and fallback behavior, state-component semantics, async button feedback, and member-removal confirmation. Existing lint, strict typecheck, component tests, production build, and Playwright discovery remain release gates. Browser accessibility and responsive runtime checks remain explicitly unverified until the full stack and browser can run.

## Scope boundaries

This increment does not claim a complete English translation, introduce a client-side locale cookie, redesign the dashboard, or add a UI framework dependency. It creates the enforced localization foundation and hardens the highest-risk interaction states.
