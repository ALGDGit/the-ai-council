using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace TheIACouncil.Services;

public sealed class OllamaDetector
{
    private readonly HttpClient _http;

    public OllamaDetector(HttpClient http) => _http = http;

    public sealed record ProbeResult(bool DaemonReachable, IReadOnlyList<string> ModelNames, string Error);

    public async Task<ProbeResult> ProbeAsync(string baseUrl, CancellationToken ct)
    {
        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/tags";
            using var res = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return new ProbeResult(false, [],
                    $"{res.StatusCode}");

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var list = new List<string>();
            foreach (var m in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                if (m.TryGetProperty("name", out var nameEl))
                {
                    var n = nameEl.GetString();
                    if (!string.IsNullOrWhiteSpace(n))
                        list.Add(n.Trim());
                }
            }

            return new ProbeResult(true, list, "");
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, [], ex.Message);
        }
    }

    /// <summary>
    /// Intenta detectar si el ejecutable <c>ollama</c> está en PATH (CLI instalado).
    /// </summary>
    public static bool TryDetectCliInstalled(out string? versionLine)
    {
        versionLine = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null)
                return false;
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(4000);
            if (p.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
                return false;
            var line = stdout.Trim().Split('\r', '\n').FirstOrDefault(l => l.Trim().Length > 0);
            if (!string.IsNullOrEmpty(line))
                versionLine = line.Trim();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
