# Local Validation

Backend CI runs on pull requests and can also be started manually. Other GitHub Actions for this repository are manual-only. Use these commands before opening or updating a PR when you want validation without spending extra Actions minutes. Run commands from the repository root unless a command says otherwise.

## Quick Check

Use this for docs-only or narrowly scoped changes:

```bash
git diff --check
if grep -RIn '<<<<<<<\|=======\|>>>>>>>' . --exclude=LOCAL_VALIDATION.md --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj --exclude-dir=TestResults; then
  echo "Merge conflict marker detected."
  exit 1
fi
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
- GitHub Actions workflows are manual-only. Previous automatic workflow versions can be restored from Git history if needed.
