# Screenshots

Portfolio screenshots captured via Playwright from running Next.js dev server.
Screenshots show unauthenticated states (login required for authenticated views).

## Captured screenshots

| File | Description |
|------|-------------|
| `landing.png` | Landing page with hero, security features, and upload processing pipeline |
| `dashboard.png` | Dashboard metrics (loading state) |
| `files.png` | File management (empty state - no workspace) |
| `shares.png` | Share management (empty state - no workspace) |
| `settings-workspace.png` | Workspace settings |
| `settings-security.png` | Security settings with TOTP/2FA setup |
| `notifications.png` | Notification center (error state) |
| `upload.png` | Upload page (workspace required) |

## Notes

- All pages show Indonesian localization
- Screenshots captured at 1440x900 viewport
- Authenticated views require login; mock data would misrepresent the UI
- Docker runtime verification (E2E tests) pending Docker availability
