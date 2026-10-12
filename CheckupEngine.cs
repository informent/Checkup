using System.IO;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using System.Diagnostics;
namespace Checkup;
public enum FindingLevel { Good, Attention, Critical }
public sealed record Finding(string Area, FindingLevel Level, string Title, string Explanation, string Evidence, bool CanRepair = false);
public static class CheckupEngine
{
    public static IReadOnlyList<Finding> Scan() => Scan(CancellationToken.None);
    public static IReadOnlyList<Finding> Scan(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var findings = new List<Finding>();
        var system = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && string.Equals(d.Name, Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase));
        if (system is not null) { var free = system.AvailableFreeSpace / 1_073_741_824d; findings.Add(free < 10 ? new Finding("Storage", FindingLevel.Attention, "Low system-drive space", "Windows may become unstable when the system drive is nearly full.", $"{free:0.0} GB available", true) : new Finding("Storage", FindingLevel.Good, "System-drive space is healthy", "There is enough free space for normal Windows operation.", $"{free:0.0} GB available")); }
        var temp = Path.GetTempPath(); var inventory = MeasureTemp(temp, cancellationToken); var tempBytes = inventory.Bytes;
        findings.Add(tempBytes > 5_000_000_000 ? new Finding("Storage", FindingLevel.Attention, "Temporary files are growing large", "Temporary files can be reviewed before cleanup. Checkup will never delete them without an explicit preview.", $"{tempBytes / 1_073_741_824d:0.0} GB in the current temp tree", true) : new Finding("Storage", FindingLevel.Good, "Temporary-file footprint is reasonable", "No unusually large temporary-file footprint was detected.", $"{tempBytes / 1_048_576d:0.0} MB measured"));
        if (!inventory.Complete) findings.Add(new Finding("Storage", FindingLevel.Attention, "Temporary-file scan has partial coverage", "Linked or unreadable entries were skipped, or the scan budget was reached. The measured size is a lower bound.", $"{inventory.Files} files measured; {inventory.Skipped} entries skipped; budget reached: {inventory.BudgetReached}"));
        cancellationToken.ThrowIfCancellationRequested();
        findings.Add(NetworkInterface.GetIsNetworkAvailable() ? new Finding("Connectivity", FindingLevel.Good, "Network is available", "Windows reports an active network interface.", "NetworkInterface.GetIsNetworkAvailable() = true") : new Finding("Connectivity", FindingLevel.Attention, "No network is currently available", "This may be intentional if the device is offline.", "No active network interface reported"));
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); var startupCount = run?.GetValueNames().Length ?? 0;
        findings.Add(new Finding("Startup", FindingLevel.Good, "Startup entries inventoried", "Checkup found the current-user startup entries without changing them.", $"{startupCount} current-user entries"));
        var activation = ReadActivationEvidence();
        findings.Add(new Finding("Activation", activation.IsActivated ? FindingLevel.Good : FindingLevel.Attention, activation.IsActivated ? "Windows reports activation is valid" : "Windows activation needs review", activation.IsActivated ? "The official Windows licensing script reports an active license." : "Checkup cannot activate Windows, but you can review the official status and troubleshoot it from Windows Settings.", activation.Evidence));
        findings.Add(new Finding("Safety", FindingLevel.Good, "Scan is read-only", "No settings, files, services, or registry values were changed.", "No repair actions were applied"));
        return findings;
    }
    private static (bool IsActivated, string Evidence) ReadActivationEvidence()
    {
        try
        {
            var info = new ProcessStartInfo { FileName = "cscript.exe", Arguments = $"//Nologo \"{Environment.SystemDirectory}\\slmgr.vbs\" /xpr", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var process = Process.Start(info); if (process is null) return (false, "Could not start the official Windows licensing script.");
            // Drain both redirected streams while the process runs. Waiting first can
            // deadlock if cscript fills either pipe before exiting.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(4000)) { try { process.Kill(true); } catch { } process.WaitForExit(); return (false, "Activation status check timed out."); }
            var output = (stdout.GetAwaiter().GetResult() + " " + stderr.GetAwaiter().GetResult()).Trim();
            var active = output.Contains("permanently activated", StringComparison.OrdinalIgnoreCase) || output.Contains("permanently activated.", StringComparison.OrdinalIgnoreCase);
            return (active, string.IsNullOrWhiteSpace(output) ? $"slmgr.vbs exited with code {process.ExitCode}" : output.Replace(Environment.NewLine, " "));
        }
        catch (Exception ex) { return (false, $"Activation status unavailable: {ex.Message}"); }
    }
    public sealed record TempInventory(long Bytes, int Files, int Skipped, bool BudgetReached)
    {
        public bool Complete => Skipped == 0 && !BudgetReached;
    }

    public static TempInventory MeasureTemp(string root, CancellationToken cancellationToken = default, int maxEntries = 200_000, TimeSpan? budget = null)
    {
        if (maxEntries < 1 || budget is { } duration && duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxEntries));
        cancellationToken.ThrowIfCancellationRequested();
        var timer = Stopwatch.StartNew(); var timeout = budget ?? TimeSpan.FromSeconds(15);
        long total = 0; var files = 0; var skipped = 0; var visited = 0;
        var pending = new Stack<string>(); pending.Push(Path.GetFullPath(root));
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (timer.Elapsed >= timeout) return new(total, files, skipped, true);
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++visited > maxEntries || timer.Elapsed >= timeout) return new(total, files, skipped, true);
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                        if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                        else { var length = new FileInfo(entry).Length; total = length > long.MaxValue - total ? long.MaxValue : total + length; files++; }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { skipped++; }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { skipped++; }
        }
        return new(total, files, skipped, false);
    }
    internal static long MeasureTempBytesForTesting(string root) => MeasureTemp(root).Bytes;
}
