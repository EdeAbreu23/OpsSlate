# Local Validation

GitHub Actions for this repository are manual-only. Use these commands before opening or updating a PR when you want validation without spending Actions minutes. Run commands from the repository root unless a command says otherwise.

## Quick Check

Use this for docs-only or narrowly scoped changes:

```bash
git diff --check
grep -RIn '<<<<<<<\|=======\|>>>>>>>' . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj --exclude-dir=TestResults
```

## Full Check

Run the relevant sections below when the change touches application behavior, Docker/deployment files, dependencies, or security-sensitive docs.

## Backend

```bash
dotnet restore src/OpsSlate/OpsSlate.csproj
dotnet build src/OpsSlate/OpsSlate.csproj --configuration Release --no-restore
```

If test projects are added, run them with:

```bash
dotnet test path/to/TestProject.csproj --configuration Release
```

## Docker

```bash
docker build -t opsslate:ci .
docker compose config
```

For a local Compose deployment check:

```bash
docker compose up --build -d
```

## Notes

- This repository does not currently have a separate frontend package.
- Keep deployment-specific `.env` values local and uncommitted.
- Use the existing environment variable names documented in the README for local and Docker configuration.
