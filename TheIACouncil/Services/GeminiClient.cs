using System.Net.Http;
using System.Text;
using System.Text.Json;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class GeminiClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly ProviderConfig _cfg;
    private readonly string _personalityId;

    public GeminiClient(HttpClient http, ProviderConfig cfg, string personalityId)
    {
        _http = http;
        _cfg = cfg;
        _personalityId = BrotherPersonalityCatalog.NormalizeId(personalityId);
    }

    public string BrotherName => "Hermano Gemini";
    public string ProviderLabel => "Google Gemini";
    public string ModelId => _cfg.Model.Trim();
    public string PersonalityId => _personalityId;

    public async Task<string> CompleteAsync(string userMessage, CancellationToken cancellationToken)
    {
        var key = Uri.EscapeDataString(_cfg.ApiKey.Trim());
        var model = Uri.EscapeDataString(_cfg.Model);
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}";

        var body = new
        {
            contents = new object[]
            {
                new { parts = new object[] { new { text = userMessage } } }
            },
            generationConfig = new { temperature = 0.35, maxOutputTokens = 380 }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini: {res.StatusCode} — {json}");

        using var doc = JsonDocument.Parse(json);
        var parts = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");
        var text = parts[0].GetProperty("text").GetString();
        return text?.Trim() ?? "";
    }
}
