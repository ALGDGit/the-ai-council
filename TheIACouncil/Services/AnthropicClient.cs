using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class AnthropicClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly ProviderConfig _cfg;
    private readonly string _personalityId;

    public AnthropicClient(HttpClient http, ProviderConfig cfg, string personalityId)
    {
        _http = http;
        _cfg = cfg;
        _personalityId = BrotherPersonalityCatalog.NormalizeId(personalityId);
    }

    public string BrotherName => "Hermano Claude";
    public string ProviderLabel => "Anthropic";
    public string ModelId => _cfg.Model.Trim();
    public string PersonalityId => _personalityId;

    public async Task<string> CompleteAsync(string userMessage, CancellationToken cancellationToken)
    {
        var url = $"{_cfg.BaseUrl.TrimEnd('/')}/v1/messages";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("x-api-key", _cfg.ApiKey.Trim());
        req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        var body = new
        {
            model = _cfg.Model,
            max_tokens = 380,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[] { new { type = "text", text = userMessage } }
                }
            }
        };

        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        var json = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Anthropic: {res.StatusCode} — {json}");

        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        return text?.Trim() ?? "";
    }
}
