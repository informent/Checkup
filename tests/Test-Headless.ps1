param([Parameter(Mandatory=$true)][string]$Exe)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('Checkup-headless-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
function Invoke-Headless([string]$Arguments, [int]$Expected) {
    $process = Start-Process -FilePath $Exe -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(150)
        while (!$process.HasExited) {
            $process.Refresh()
            if ($process.MainWindowHandle -ne 0) { throw 'Unattended command opened a desktop window' }
            if ([DateTime]::UtcNow -ge $deadline) { throw 'Unattended command timed out' }
            Start-Sleep -Milliseconds 100
        }
        $process.WaitForExit()
        if ($process.ExitCode -ne $Expected) { throw "Expected exit $Expected, received $($process.ExitCode)" }
    }
    finally { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
}
try {
    $report = Join-Path $root 'support.zip'
    $arguments = '--scan --output "' + $report + '"'
    Invoke-Headless $arguments 0
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($report)
    try {
        if ($archive.Entries.Count -ne 4 -or !$archive.GetEntry('report.json') -or !$archive.GetEntry('manifest-sha256.txt')) { throw 'Support bundle is incomplete' }
        $reader = [IO.StreamReader]::new($archive.GetEntry('report.json').Open())
        try { $json = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($json.Findings.Count -lt 4) { throw 'Health findings missing' }
    } finally { $archive.Dispose() }
    $before = (Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash
    Invoke-Headless $arguments 2
    if ((Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash -ne $before) { throw 'Existing report changed' }
    Invoke-Headless '--invalid' 2
    Write-Output 'PASS: packaged unattended workflow, report content, existing-output protection, argument exits and no desktop window'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or !([IO.Path]::GetFileName($resolved)).StartsWith('Checkup-headless-')) { throw 'Unsafe cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
