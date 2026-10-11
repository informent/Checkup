# Checkup

Checkup is a read-only-first Windows health and troubleshooting tool.

It scans for useful evidence: system-drive space, temporary-file growth, network availability, startup inventory, activation status, and scan safety. It then explains what it found in plain language and calculates a transparent 0–100 health score.

## Support bundles

Checkup 1.0 exports a self-contained ZIP for troubleshooting and handoff. Each bundle contains structured JSON, a standalone HTML report, a concise system summary, a privacy note, and a SHA-256 manifest. User-profile names and product-key-shaped values are redacted. Checkup does not include file contents or make system changes while generating a bundle.

## Activation

Checkup includes a **Fix activation** action that opens Windows' official Activation settings. It does not bypass licensing, hide activation notices, or modify protected licensing components.

## Principles

- Scan before changing anything
- Explain evidence and impact
- Preview repairs before applying them
- Keep changes reversible
- No account, telemetry, or cloud service required

## Build and test

```powershell
dotnet run --project tests/CheckupEngineTests.csproj
dotnet build Checkup.csproj -c Release
dotnet publish Checkup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Checkup is not affiliated with Microsoft or Windows.

## Name change

Checkup replaces OpenFix. New builds use Checkup.exe and Checkup.csproj. Existing releases remain available under their original names.
