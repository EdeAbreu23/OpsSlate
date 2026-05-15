# Local Validation

Use these commands before pushing when you want local validation without relying on GitHub Actions. Run commands from the repository root unless a command says otherwise.

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
