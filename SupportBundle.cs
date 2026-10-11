using System.IO.Compression;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Checkup;

public sealed record SystemSummary(string OperatingSystem, string Architecture, string Runtime, long UptimeMinutes, long MemoryMegabytes);
public sealed record HealthReport(DateTimeOffset CreatedUtc, int Score, string Grade, SystemSummary System, IReadOnlyList<Finding> Findings);
public sealed record BundleResult(string Path, string Sha256, long SizeBytes);

public static class SupportBundle
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static HealthReport CreateReport(IReadOnlyList<Finding> findings, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(findings);
        var penalty = findings.Sum(f => f.Level switch { FindingLevel.Critical => 30, FindingLevel.Attention => 12, _ => 0 });
        var score = Math.Max(0, 100 - penalty);
        var grade = score switch { >= 90 => "A", >= 80 => "B", >= 70 => "C", >= 60 => "D", _ => "F" };
        var summary = new SystemSummary(
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Math.Max(0, Environment.TickCount64 / 60_000),
            Math.Max(0, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1_048_576));
        var safeFindings = findings.Select(f => f with { Evidence = Redact(f.Evidence) }).ToArray();
        return new HealthReport(now ?? DateTimeOffset.UtcNow, score, grade, summary, safeFindings);
    }

    public static BundleResult Export(string destination, IReadOnlyList<Finding> findings)
    {
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("A destination is required.", nameof(destination));
        var report = CreateReport(findings);
        var json = JsonSerializer.Serialize(report, JsonOptions);
        var html = RenderHtml(report);
        var readme = "Checkup support bundle\r\n\r\nThis bundle was generated read-only. It contains a system summary and Checkup findings, but no user name, full file paths, product keys, or file contents. Verify entries against manifest-sha256.txt before relying on them.\r\n";
        var files = new Dictionary<string, byte[]>
        {
            ["report.json"] = Encoding.UTF8.GetBytes(json),
            ["report.html"] = Encoding.UTF8.GetBytes(html),
            ["README.txt"] = Encoding.UTF8.GetBytes(readme)
        };
        var manifest = string.Join("\r\n", files.OrderBy(x => x.Key).Select(x => $"{Convert.ToHexString(SHA256.HashData(x.Value)).ToLowerInvariant()}  {x.Key}")) + "\r\n";
        files["manifest-sha256.txt"] = Encoding.UTF8.GetBytes(manifest);

        var fullPath = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
                foreach (var file in files.OrderBy(x => x.Key))
                {
                    var entry = archive.CreateEntry(file.Key, CompressionLevel.Optimal);
                    entry.LastWriteTime = report.CreatedUtc;
                    using var stream = entry.Open();
                    stream.Write(file.Value);
                }
            File.Move(temporary, fullPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        var bytes = File.ReadAllBytes(fullPath);
        return new BundleResult(fullPath, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.LongLength);
    }

    public static string RenderHtml(HealthReport report)
    {
        static string H(string value) => WebUtility.HtmlEncode(value);
        var rows = string.Join("", report.Findings.Select(f => $"<tr><td>{H(f.Area)}</td><td class='{f.Level.ToString().ToLowerInvariant()}'>{H(f.Level.ToString())}</td><td><strong>{H(f.Title)}</strong><br>{H(f.Explanation)}</td><td>{H(Redact(f.Evidence))}</td></tr>"));
        return $"<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Checkup health report</title><style>body{{font:15px system-ui;margin:40px auto;max-width:1100px;color:#18202a;padding:0 20px}}header{{background:#12233f;color:white;padding:28px;border-radius:14px}}.score{{font-size:42px;font-weight:800}}table{{border-collapse:collapse;width:100%;margin-top:24px}}th,td{{text-align:left;padding:12px;border-bottom:1px solid #dce2ea;vertical-align:top}}.good{{color:#087f5b}}.attention{{color:#b25d00}}.critical{{color:#c92a2a}}small{{color:#5c6773}}</style><header><h1>Checkup health report</h1><div class='score'>{report.Score}/100 · Grade {H(report.Grade)}</div><div>Generated {report.CreatedUtc:u}</div></header><p><strong>System:</strong> {H(report.System.OperatingSystem)} · {H(report.System.Architecture)} · uptime {report.System.UptimeMinutes} minutes · {report.System.MemoryMegabytes} MB available memory</p><table><thead><tr><th>Area</th><th>Status</th><th>Finding</th><th>Evidence</th></tr></thead><tbody>{rows}</tbody></table><p><small>Checkup generated this report without changing system state. Sensitive path and product-key patterns are redacted.</small></p></html>";
    }

    internal static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var user = Environment.UserName;
        var result = string.IsNullOrWhiteSpace(user) ? value : value.Replace(user, "[user]", StringComparison.OrdinalIgnoreCase);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile)) result = result.Replace(profile, "[user-profile]", StringComparison.OrdinalIgnoreCase);
        return System.Text.RegularExpressions.Regex.Replace(result, @"\b[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}\b", "[product-key-redacted]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
