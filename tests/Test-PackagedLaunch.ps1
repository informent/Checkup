param([Parameter(Mandatory=$true)][string]$Exe)
$ErrorActionPreference = 'Stop'
$process = Start-Process -FilePath $Exe -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 3
    $process.Refresh()
    if ($process.HasExited -or $process.MainWindowHandle -eq 0) { throw 'Downloaded Checkup did not create its desktop window' }
    Write-Output 'PASS: exact downloaded Checkup desktop executable starts successfully'
}
finally { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
