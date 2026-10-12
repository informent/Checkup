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

## Unattended support bundles

Version 1.2.0 can generate a private support ZIP without opening a window:

```powershell
$p = Start-Process .\Checkup.exe -WindowStyle Hidden -ArgumentList '--scan --output "C:\Reports\checkup.zip"' -Wait -PassThru
$p.ExitCode
```

Use a new ZIP filename. Existing reports are never replaced by this command. Exit 0 means the report was written, 2 means invalid arguments or an existing output, and 1 means the operation failed. Findings inside the report determine health; a successful export does not mean the machine has no issues.

Temporary-file inventory now skips links and junctions, supports cancellation, and stops after 200,000 entries or 15 seconds of enumeration. Partial coverage creates an attention finding with measured-file and skipped-entry counts, so a lower-bound size is not mistaken for a complete scan. Filesystem calls already in progress can outlast the enumeration budget. Bundles remain local and should be reviewed before sharing.

## Automatic setup

Windows setup packages install Checkup for your current account, bundle the .NET runtime, create a Start menu shortcut and register an uninstaller. A desktop shortcut is optional. No administrator password or manual file copying is required. Existing settings and saved data stay in their current locations. The portable ZIP remains available.

Download `Checkup-v1.2.0-setup.exe` from Releases and run it.

For unattended deployment:

```powershell
Start-Process .\Checkup-v1.2.0-setup.exe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait -PassThru
```

Build from source with the [open-source Inno Setup compiler](https://github.com/jrsoftware/issrc) installed:

```powershell
pwsh -File scripts/Build-Setup.ps1 -Verify
```

The verification option installs into a temporary test folder, checks reinstall and launch, then uninstalls. It runs on the hosted Windows runner during CI. It does not remove existing user settings or backups.
