# Homelab Automation Center

A health-aware automation dashboard for homelabs (Unraid-friendly).

## Problem

Most tools only answer:
- “Did my script run?”

They don’t answer:
- Was it healthy?
- Did it have warnings?
- Is it stale?
- Should dependent jobs run?

## Solution

This project introduces a simple automation control layer:

- Status tracking via `status.json`
- Health-aware execution (block on errors, allow warnings)
- Stale detection per job
- Central dashboard
- ntfy + monitoring integration

## Features (Prototype)

- SUCCESS / WARNING / ERROR / STALE states
- Script-level logs
- Health pre-checks
- Dashboard generation

## Roadmap (v1)

- Dockerized dashboard UI
- jobs.yml config (per-script intervals)
- warning/error behavior controls
- ntfy + Uptime Kuma integration
- Unraid-friendly deployment

## Example status.json

```json
{
  "status": "success",
  "message": "Backup completed",
  "runtime": 12,
  "last_run": "2026-05-04T16:23:36"
}
```

## Vision

A lightweight automation control plane for homelabs.

Not just “did it run?” — but “should it run?”

