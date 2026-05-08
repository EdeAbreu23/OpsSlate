# Homelab Automation Center v1

Simple self-hostable dashboard for homelab job health status.

## Features

- ASP.NET Core Razor Pages (.NET 8)
- Reads jobs from `${HAC_CONFIG_PATH}` (default `/config/jobs.yml`) with YamlDotNet
- Reads each job's JSON status file from configured `status_path`
- Dashboard auto-refreshes every 60 seconds
- Simple per-job detail page with current status, config path, and dependency details
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
dotnet build src/HomelabAutomationCenter/HomelabAutomationCenter.csproj
dotnet run --project src/HomelabAutomationCenter/HomelabAutomationCenter.csproj
```

Then open `http://localhost:5087` (or the URL shown in logs).

## Docker

```bash
docker build -t homelab-automation-center:latest .
docker run --rm -p 8080:8080 \
  -v $(pwd)/config:/config \
  -v $(pwd)/status:/status \
  homelab-automation-center:latest
```

## Docker Compose

```bash
docker compose up --build -d
```

The Compose deployment publishes the app on host port `8099` and attaches the container to an existing external Docker network. By default, Homelab Automation Center joins `media_network`, which is useful on Unraid when shared services and monitoring containers already use that network.

If your shared Docker network has a different name, set `HOMELAB_AUTOMATION_CENTER_DOCKER_NETWORK` before deploying. Example `.env`:

```env
HOMELAB_AUTOMATION_CENTER_DOCKER_NETWORK=media_network
HAC_CONFIG_PATH=/config/jobs.yml
HAC_STATUS_ROOT=/status
```

Deploy notes after changing networks:

```bash
docker compose down
docker compose up --build -d
```

If the old Compose-created default network remains unused, it can be removed:

```bash
docker network rm homelab-automation-center_default
```

If Docker reports that the network is still in use, inspect the containers attached to it before removing the network.

Mounts:
- `/config:/config`
- `/status:/status`

## Path configuration

Homelab Automation Center supports environment-configurable filesystem paths while keeping Docker defaults unchanged:

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `HAC_CONFIG_PATH` | `/config/jobs.yml` | YAML file containing dashboard job definitions. |
| `HAC_STATUS_ROOT` | `/status` | Root directory used to resolve relative `status_path` values from the jobs config. Absolute `status_path` values continue to be used as-is. |

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
    status_path: /status/health_check/status.json
    stale_after_minutes: 60
  - id: backup_nas
    name: NAS Backup
    status_path: /status/backup_nas/status.json
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

## Current UI capabilities

- The dashboard uses a simple meta refresh and reloads every 60 seconds.
- Each dashboard row links to a detail page showing ID, name, final status, reason, raw status, last run, runtime, message, warnings, errors, stale state, file found state, configured status path, and `depends_on` values.
- Detail pages include a placeholder for future history/timeline support. No database or persistence is implemented yet.

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
