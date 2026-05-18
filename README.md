# OpsSlate v1

Simple self-hostable dashboard for job health status.

## Features

- ASP.NET Core Razor Pages (.NET 8)
- Reads jobs from `${HAC_CONFIG_PATH}` (default `/config/jobs.yml`) with YamlDotNet
- Reads each job's JSON status file from configured `status_path`
- Dashboard auto-refreshes every 60 seconds
- Simple per-job detail page with current status and dependency details
- Final status precedence:
  1. missing status file = `UNKNOWN`
  2. invalid JSON = `UNKNOWN`
  3. errors > 0 or raw status `error` = `ERROR`
  4. stale = `STALE`
  5. warnings > 0 or raw status `warning` = `WARNING`
  6. otherwise `SUCCESS`
  7. dependency check: if any configured `depends_on` job is not `SUCCESS`, the dependent job is shown as `BLOCKED`

## Local run

```bash
dotnet build src/OpsSlate/OpsSlate.csproj
dotnet run --project src/OpsSlate/OpsSlate.csproj
```

Then open `http://localhost:5087` (or the URL shown in logs).

## CI

OpsSlate keeps routine GitHub Actions limited. The required Backend CI workflow runs on pull requests and can also be started manually; other hosted workflows remain manual-only to avoid routine Actions minute usage. Run local validation before opening or updating PRs.

- **Backend CI** runs on pull requests targeting `main` and on manual dispatch. It restores, builds, and runs test projects when present for `src/OpsSlate/OpsSlate.csproj` with the .NET 8 SDK.
- **Docker CI** builds the Docker image with `docker build -t opsslate:ci .`. It does not publish or push images.
- **CodeQL** runs GitHub CodeQL analysis for C# when manually dispatched.
- **Dependabot** routine version PRs are disabled with `open-pull-requests-limit: 0`; keep Dependabot alerts and security updates enabled in GitHub repository settings.

Backend CI safely checks for test projects before running tests. If no test project exists, CI skips `dotnet test` without failing; once test projects are added, they will run automatically.

Frontend CI is intentionally not added yet because this repo does not have a separate frontend project.


## Deployment protection

OpsSlate v1 is built for trusted LAN and self-hosted deployments unless you intentionally place protective access controls in front of it. Anyone who can reach the web UI can add, edit, or delete job configuration through `/Jobs/New`, `/Jobs/Edit/{id}`, and `/Jobs/Delete/{id}`. Do not expose OpsSlate directly to the public internet.

Recommended protection options include:

- Reverse proxy basic authentication.
- Authelia or Authentik in front of the app.
- Tailscale or VPN-only access.
- Firewall rules or LAN allowlists that restrict who can reach the container port.
- Cloudflare Access only when you intentionally expose OpsSlate through Cloudflare.

OpsSlate includes a lightweight built-in rate limit for unsafe HTTP methods to slow repeated write attempts, but this is not authentication. Behind a reverse proxy, forward the real client IP correctly if you want per-client rate limiting instead of limiting by proxy address.

## Docker

```bash
docker build -t opsslate:latest .
docker run --rm -p 8080:8080 \
  -v $(pwd)/config:/config \
  -v $(pwd)/status:/status \
  opsslate:latest
```

## Docker Compose

```bash
docker compose up --build -d
```

The Compose deployment publishes the app on host port `8099` and attaches the container to an existing external Docker network. By default, OpsSlate joins `media_network`; set a different network name before deploying if your Docker host uses another shared network.

If your shared Docker network has a different name, set `OPSSLATE_DOCKER_NETWORK` before deploying. Existing deployments that already use `HOMELAB_AUTOMATION_CENTER_DOCKER_NETWORK` continue to work for backward compatibility.

OpsSlate also includes Unraid Docker labels for the app icon and WebUI link. The Compose file reads `OPSSLATE_WEBUI_URL` from `.env`, so Unraid can open a configured LAN URL. If `OPSSLATE_WEBUI_URL` is not set, the label falls back to the Unraid host/port placeholder (`http://[IP]:[PORT:8080]`) so the WebUI button resolves to the Unraid host instead of the administrator's client machine. The icon defaults to the public OpsSlate icon in the `EdeAbreu23/unraid-templates` repository, and can be overridden with `OPSSLATE_ICON_URL` if needed. Example `.env`:

```env
OPSSLATE_DOCKER_NETWORK=example_network
HAC_CONFIG_PATH=/config/jobs.yml
HAC_STATUS_ROOT=/status
OPSSLATE_WEBUI_URL=http://[IP]:[PORT:8080]
OPSSLATE_ICON_URL=https://raw.githubusercontent.com/EdeAbreu23/unraid-templates/main/images/opsslate-icon.png
```

Deploy notes after changing networks:

```bash
docker compose down
docker compose up --build -d
```

If the old Compose-created default network remains unused, it can be removed:

```bash
docker network rm opsslate_default
```

If Docker reports that the network is still in use, inspect the containers attached to it before removing the network.

Mounts:
- `/config:/config`
- `/status:/status`

## Path configuration

OpsSlate supports environment-configurable filesystem paths while keeping Docker defaults unchanged:

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `HAC_CONFIG_PATH` | `/config/jobs.yml` | YAML file containing dashboard job definitions. |
| `HAC_STATUS_ROOT` | `/status` | Root directory used to resolve `status_path` values from the jobs config. Resolved status files must stay under this directory. |
| `OPSSLATE_WEBUI_URL` | `http://[IP]:[PORT:8080]` | Optional Unraid Docker WebUI label URL. Keep the placeholder in examples; set a deployment-specific URL only in your uncommitted local `.env`. |
| `OPSSLATE_ICON_URL` | `https://raw.githubusercontent.com/EdeAbreu23/unraid-templates/main/images/opsslate-icon.png` | Optional Unraid Docker icon label URL. |

For the default Docker and Docker Compose mounts, no path environment variables are required. To run with different mount points, set the variables and update your volume mappings accordingly. Example:

```env
HAC_CONFIG_PATH=/app-config/jobs.yml
HAC_STATUS_ROOT=/job-status
```

## Config format (`${HAC_CONFIG_PATH}`; default `/config/jobs.yml`)

```yaml
jobs:
  - id: health_check
    name: Health Check
    status_path: health_check/status.json
    stale_after_minutes: 60
  - id: backup_nas
    name: NAS Backup
    status_path: backup_nas/status.json
    stale_after_minutes: 180
    depends_on:
      - health_check
    # Future notification preparation only; not sent by this release.
    # notify_on_error: true
    # notify_on_warning: false
    # notify_on_stale: true
    # Future manual run preparation only; commands are not executed by this release.
    # manual_run_enabled: false
    # run_command: /scripts/backup_nas.sh
```

Jobs may include an optional `depends_on` list. Dependency checks run after each job status is evaluated normally. If any listed dependency is missing or has a final status other than `SUCCESS`, the dependent job receives final status `BLOCKED` with a reason such as `Blocked by health_check` or `Blocked by health_check, recyclarr`.


## Adding jobs from the UI

Use **Add Job** on the dashboard to open `/Jobs/New` and create a job without manually editing `${HAC_CONFIG_PATH}`. The wizard collects a job ID, display name, status path, stale threshold, and optional comma-separated dependency IDs.

On save, OpsSlate backs up the configured jobs file to `<jobs file path>.bak.<timestamp>`, appends the new job to `jobs.yml`, preserves existing job entries and `depends_on` values, creates the status directory when possible, and writes a starter `status.json` if one does not already exist. Status paths must resolve under `${HAC_STATUS_ROOT}`; relative paths such as `backup_nas/status.json` are recommended.

The wizard validates that job IDs are required, unique ignoring case, and contain only lowercase letters, numbers, underscores, or dashes. It also requires a job name and status path, enforces a positive `stale_after_minutes` value, and checks that dependencies reference existing jobs, are not duplicated, and do not point back to the new job.


## Editing jobs from the UI

Use each dashboard row's **Edit** link or the **Edit Job** link on a job detail page to open `/Jobs/Edit/{id}`. The v1 edit workflow shows the selected job ID as read-only and lets you update the display name, status path, stale threshold, and comma-separated dependency IDs.

On save, OpsSlate backs up `${HAC_CONFIG_PATH}` to `<jobs file path>.bak.<timestamp>`, updates only the selected job, preserves all other jobs and unedited YAML fields, and keeps the existing job order. If the status path changes, the app validates that it resolves under `${HAC_STATUS_ROOT}`, creates the status directory when possible, and writes a starter `status.json` only when the new status file does not already exist. Scripts are never edited or executed by the edit workflow.

The edit form validates that the job name and status path are required, `stale_after_minutes` is greater than zero, dependencies reference existing job IDs, dependencies do not include the job itself, and duplicate dependencies are rejected.


## Deleting jobs from the UI

Use each dashboard row's **Delete** link or the **Delete Job** link on a job detail page to open `/Jobs/Delete/{id}`. Deletion always uses a confirmation page; the app never deletes a job from a GET request.

The confirmation page shows the job ID, name, final status, current `depends_on` values, jobs that depend on the selected job, and whether the status file currently exists. If any other jobs depend on the selected job, deletion is blocked by default and the dependent jobs are listed. You can force deletion from the confirmation page when you intentionally want to leave those other jobs and their `depends_on` values unchanged.

On delete, OpsSlate backs up the configured jobs file to `<jobs file path>.bak.<timestamp>` before removing the selected job entry. All other job entries and their `depends_on` values are preserved.

Status cleanup is optional. If **Also delete status file/folder** is checked, only the deleted job's resolved status file under `${HAC_STATUS_ROOT}` is removed when it exists. The parent status folder is removed only if it is empty after the file delete; non-empty folders are not recursively deleted. Scripts are never deleted, edited, or executed by the delete workflow.

## Current UI capabilities

- The dashboard uses a simple meta refresh and reloads every 60 seconds.
- Each dashboard row links to a detail page showing ID, name, final status, reason, raw status, last run, runtime, message, warnings, errors, stale state, file found state, configured dependencies, and `depends_on` values.
- Detail pages include a placeholder for future history/timeline support. No database or persistence is implemented yet.


## Roadmap notes

Future job-management ideas, not implemented in this release:

- Script Import Assistant
- Script Inspector / Code Validator
- Safe Run button
- Script patch preview

## Future ntfy notification preparation

This release does **not** send notifications or make outbound HTTP calls. Future configs may use fields such as `notify_on_error`, `notify_on_warning`, and `notify_on_stale` to decide when ntfy messages should be sent. These fields are documentation placeholders only today.

## Future manual run preparation

This release does **not** execute scripts, schedule jobs, or provide manual run buttons. Future configs may use fields such as `manual_run_enabled` and `run_command` to describe safe manual execution behavior. These fields are documentation placeholders only today.

## Status JSON example

```json
{
  "status": "success",
  "last_run": "2026-05-05T09:30:00Z",
  "runtime": "00:00:08",
  "message": "Health checks completed successfully.",
  "warnings": 0,
  "errors": 0
}
```

## Security notes

OpsSlate v1 is intended for trusted, self-hosted networks. The add, edit, and delete job pages can modify the configured jobs file and do not include end-user authentication yet; do not expose the app directly to the public internet. Protect access with a reverse proxy, Authelia or Authentik, Tailscale or VPN-only access, firewall or LAN allowlists, or Cloudflare Access when intentionally publishing through Cloudflare. Before any broader internet-facing deployment, add authentication/authorization, CSRF/session hardening review, and role-based controls for write operations.

The app does not execute configured commands, does not send notifications, and validates UI-submitted status paths so they resolve under `HAC_STATUS_ROOT` before creating or deleting status files.
