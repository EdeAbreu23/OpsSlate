# Security

OpsSlate is intended for trusted, self-hosted network use. The current write pages can add, edit, and delete job configuration without end-user authentication.

## Deployment Protection

OpsSlate v1 is trusted LAN and self-hosted software unless protected by external access controls. Anyone who can reach the web UI can add, edit, or delete job configuration through `/Jobs/New`, `/Jobs/Edit/{id}`, and `/Jobs/Delete/{id}`. Do not expose OpsSlate directly to the public internet.

Recommended protection options include:

- Reverse proxy basic authentication.
- Authelia or Authentik in front of the app.
- Tailscale or VPN-only access.
- Firewall rules or LAN allowlists that restrict who can reach the container port.
- Cloudflare Access only when you intentionally expose OpsSlate through Cloudflare.

OpsSlate includes a lightweight built-in rate limit for unsafe HTTP methods to slow repeated write attempts, but this is not authentication. Behind a reverse proxy, forward the real client IP correctly if you want per-client rate limiting instead of limiting by proxy address.

## Configuration

- Keep deployment-specific `.env` values local and uncommitted.
- Preserve the existing `HAC_*` environment variable names for backward compatibility.
- Status paths must remain constrained under `HAC_STATUS_ROOT`.

## Local Checks

Run the relevant commands in `docs/LOCAL_VALIDATION.md` before opening or updating a PR. Backend CI runs on pull requests, and other GitHub Actions workflows should be run intentionally when needed.
