using System.Net.Http;
using System.Text;
using System.Text.Json;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class OllamaClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly ProviderConfig _cfg;
    private readonly string _model;
    private readonly string _personalityId;

    public OllamaClient(HttpClient http, ProviderConfig cfg, string modelName, string brotherDisplayName,
        string personalityId)
    {
        _http = http;
        _cfg = cfg;
        _model = modelName.Trim();
        BrotherName = brotherDisplayName;
        _personalityId = BrotherPersonalityCatalog.NormalizeId(personalityId);
    }

    public string BrotherName { get; }
    public string ProviderLabel => $"Ollama · {_model}";
    public string ModelId => _model;
    public string PersonalityId => _personalityId;

    public async Task<string> CompleteAsync(string userMessage, CancellationToken cancellationToken)
    {
        var baseUrl = _cfg.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/chat";
        var body = new
        {
            model = _model,
            messages = new object[] { new { role = "user", content = userMessage } },
            stream = false,
            options = new { temperature = 0.35, num_predict = 380 }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama ({_model}): {res.StatusCode} — {json}");

        using var doc = JsonDocument.Parse(json);
        var msg = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
        return msg?.Trim() ?? "";
    }
}
