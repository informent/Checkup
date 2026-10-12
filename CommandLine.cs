using System.IO;
namespace Checkup;

public static class CommandLine
{
    public static int Run(string[] args)
    {
        if (args.Length != 3 || args[0] != "--scan" || args[1] != "--output" || string.IsNullOrWhiteSpace(args[2]) || !args[2].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return 2;
        try
        {
            if (File.Exists(args[2])) return 2;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var findings = CheckupEngine.Scan(timeout.Token);
            SupportBundle.Export(args[2], findings, overwrite: false);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OperationCanceledException or System.Security.SecurityException) { return 1; }
    }
}
