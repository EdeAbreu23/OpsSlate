# Homelab Automation Center v1

Simple self-hostable dashboard for homelab job health status.

## Features

- ASP.NET Core Razor Pages (.NET 8)
- Reads jobs from `/config/jobs.yml` with YamlDotNet
- Reads each job's JSON status file from configured `status_path`
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

Mounts:
- `/config:/config`
- `/status:/status`

## Config format (`/config/jobs.yml`)

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
```

Jobs may include an optional `depends_on` list. Dependency checks run after each job status is evaluated normally. If any listed dependency is missing or has a final status other than `SUCCESS`, the dependent job receives final status `BLOCKED` with a reason such as `Blocked by health_check` or `Blocked by health_check, recyclarr`.

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
