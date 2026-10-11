using System.IO;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using System.Diagnostics;
namespace Checkup;
public enum FindingLevel { Good, Attention, Critical }
public sealed record Finding(string Area, FindingLevel Level, string Title, string Explanation, string Evidence, bool CanRepair = false);
public static class CheckupEngine
{
    public static IReadOnlyList<Finding> Scan()
    {
        var findings = new List<Finding>();
        var system = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && string.Equals(d.Name, Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase));
        if (system is not null) { var free = system.AvailableFreeSpace / 1_073_741_824d; findings.Add(free < 10 ? new Finding("Storage", FindingLevel.Attention, "Low system-drive space", "Windows may become unstable when the system drive is nearly full.", $"{free:0.0} GB available", true) : new Finding("Storage", FindingLevel.Good, "System-drive space is healthy", "There is enough free space for normal Windows operation.", $"{free:0.0} GB available")); }
        var temp = Path.GetTempPath(); var tempBytes = MeasureTempBytes(temp);
        findings.Add(tempBytes > 5_000_000_000 ? new Finding("Storage", FindingLevel.Attention, "Temporary files are growing large", "Temporary files can be reviewed before cleanup. Checkup will never delete them without an explicit preview.", $"{tempBytes / 1_073_741_824d:0.0} GB in the current temp tree", true) : new Finding("Storage", FindingLevel.Good, "Temporary-file footprint is reasonable", "No unusually large temporary-file footprint was detected.", $"{tempBytes / 1_048_576d:0.0} MB measured"));
        findings.Add(NetworkInterface.GetIsNetworkAvailable() ? new Finding("Connectivity", FindingLevel.Good, "Network is available", "Windows reports an active network interface.", "NetworkInterface.GetIsNetworkAvailable() = true") : new Finding("Connectivity", FindingLevel.Attention, "No network is currently available", "This may be intentional if the device is offline.", "No active network interface reported"));
        var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); var startupCount = run?.GetValueNames().Length ?? 0;
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
    private static long SafeLength(string path) { try { return new FileInfo(path).Length; } catch { return 0; } }
    private static long MeasureTempBytes(string root)
    {
        if (!Directory.Exists(root)) return 0;
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try { foreach (var file in Directory.EnumerateFiles(directory)) total = SaturatingAdd(total, SafeLength(file)); } catch { }
            try { foreach (var child in Directory.EnumerateDirectories(directory)) pending.Push(child); } catch { }
        }
        return total;
    }
    internal static long MeasureTempBytesForTesting(string root) => MeasureTempBytes(root);
    private static long SaturatingAdd(long total, long value) => value > long.MaxValue - total ? long.MaxValue : total + value;
}
