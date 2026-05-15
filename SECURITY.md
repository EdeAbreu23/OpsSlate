# Security

OpsSlate is intended for trusted, self-hosted network use. The current write pages can add, edit, and delete job configuration without end-user authentication.

## Exposure Boundary

- Do not expose OpsSlate directly to the public internet in its current form.
- Add reverse-proxy authentication, SSO, or app-native authentication before any internet-facing deployment.
- Treat access to the web UI as access to modify the configured jobs file.

## Configuration

- Keep deployment-specific `.env` values local and uncommitted.
- Preserve the existing `HAC_*` environment variable names for backward compatibility.
- Status paths must remain constrained under `HAC_STATUS_ROOT`.

## Local Checks

Run the relevant commands in `docs/LOCAL_VALIDATION.md` before opening or updating a PR. Backend CI runs on pull requests, and other GitHub Actions workflows should be run intentionally when needed.
