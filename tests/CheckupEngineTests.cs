using Checkup;
using System.IO.Compression;
using System.Security.Cryptography;
var findings = CheckupEngine.Scan();
if (findings.Count < 4) throw new Exception("Expected core system checks.");
if (!findings.Any(f => f.Area == "Safety" && f.Level == FindingLevel.Good)) throw new Exception("Read-only safety finding missing.");
Console.WriteLine($"PASS: Checkup scan returned {findings.Count} checks");

var tempTree = Path.Combine(Path.GetTempPath(), $"openfix-tree-{Guid.NewGuid():N}");
try
{
    Directory.CreateDirectory(Path.Combine(tempTree, "one", "two"));
    File.WriteAllBytes(Path.Combine(tempTree, "root.bin"), new byte[7]);
    File.WriteAllBytes(Path.Combine(tempTree, "one", "nested.bin"), new byte[11]);
    File.WriteAllBytes(Path.Combine(tempTree, "one", "two", "deep.bin"), new byte[13]);
    if (CheckupEngine.MeasureTempBytesForTesting(tempTree) != 31) throw new Exception("Temporary-tree scan missed nested files.");
}
finally { if (Directory.Exists(tempTree)) Directory.Delete(tempTree, true); }
Console.WriteLine("PASS: temporary-tree measurement includes nested files");

var sample = new[]
{
    new Finding("Storage", FindingLevel.Good, "Healthy", "All good", "80 GB available"),
    new Finding("Connectivity", FindingLevel.Attention, "Offline", "Check the connection", $@"C:\Users\{Environment.UserName}\secret ABCDE-FGHIJ-KLMNO-PQRST-UVWXY")
};
var report = SupportBundle.CreateReport(sample, DateTimeOffset.Parse("2026-01-02T03:04:05Z"));
if (report.Score != 88 || report.Grade != "B") throw new Exception("Health score or grade is incorrect.");
var html = SupportBundle.RenderHtml(report);
if (html.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase) || html.Contains("ABCDE-FGHIJ", StringComparison.OrdinalIgnoreCase)) throw new Exception("HTML report leaked sensitive data.");

var output = Path.Combine(Path.GetTempPath(), $"openfix-test-{Guid.NewGuid():N}.zip");
try
{
    var bundle = SupportBundle.Export(output, sample);
    if (!File.Exists(bundle.Path) || bundle.SizeBytes == 0 || bundle.Sha256.Length != 64) throw new Exception("Support bundle result is invalid.");
    using var zip = ZipFile.OpenRead(output);
    var expected = new HashSet<string>(new[] { "README.txt", "manifest-sha256.txt", "report.html", "report.json" }, StringComparer.Ordinal);
    if (!expected.SetEquals(zip.Entries.Select(e => e.FullName))) throw new Exception("Support bundle entries are incomplete.");
    var manifestEntry = zip.GetEntry("manifest-sha256.txt") ?? throw new Exception("Manifest missing.");
    string manifest; using (var reader = new StreamReader(manifestEntry.Open())) manifest = reader.ReadToEnd();
    var jsonEntry = zip.GetEntry("report.json") ?? throw new Exception("JSON report missing.");
    string json; using (var reader = new StreamReader(jsonEntry.Open())) json = reader.ReadToEnd();
    if (json.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase) || json.Contains("ABCDE-FGHIJ", StringComparison.OrdinalIgnoreCase)) throw new Exception("JSON report leaked sensitive data.");
    foreach (var entry in zip.Entries.Where(e => e.FullName != "manifest-sha256.txt"))
    {
        using var stream = entry.Open(); using var memory = new MemoryStream(); stream.CopyTo(memory);
        var hash = Convert.ToHexString(SHA256.HashData(memory.ToArray())).ToLowerInvariant();
        if (!manifest.Contains($"{hash}  {entry.FullName}", StringComparison.Ordinal)) throw new Exception($"Manifest hash mismatch: {entry.FullName}");
    }
}
finally { if (File.Exists(output)) File.Delete(output); }
Console.WriteLine("PASS: support bundle scoring, redaction, contents, and hashes verified");
