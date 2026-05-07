using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class OpenAICompatibleClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly ProviderConfig _cfg;
    private readonly string _brother;
    private readonly string _label;
    private readonly string _personalityId;

    public OpenAICompatibleClient(HttpClient http, ProviderConfig cfg, string brotherName, string label,
        string personalityId)
    {
        _http = http;
        _cfg = cfg;
        _brother = brotherName;
        _label = label;
        _personalityId = BrotherPersonalityCatalog.NormalizeId(personalityId);
    }

    public string BrotherName => _brother;
    public string ProviderLabel => _label;
    public string ModelId => _cfg.Model.Trim();
    public string PersonalityId => _personalityId;

    public async Task<string> CompleteAsync(string userMessage, CancellationToken cancellationToken)
    {
        var baseUrl = _cfg.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/chat/completions";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.ApiKey.Trim());

        var body = new
        {
            model = _cfg.Model,
            messages = new object[] { new { role = "user", content = userMessage } },
            temperature = 0.35,
            max_tokens = 380
        };

        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"{_label}: {res.StatusCode} — {json}");

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content")
            .GetString();
        return content?.Trim() ?? "";
    }
}
