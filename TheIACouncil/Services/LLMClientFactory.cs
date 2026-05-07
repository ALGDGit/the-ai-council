using System.Linq;
using System.Net.Http;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class LLMClientFactory
{
    private readonly HttpClient _http;

    public LLMClientFactory(HttpClient http) => _http = http;

    public IReadOnlyList<ILLMClient> CreateEnabledClients(AppSettings settings)
    {
        var http = _http;
        var list = new List<ILLMClient>();

        foreach (var p in settings.Providers.Where(x => x.Enabled))
        {
            switch (p.Kind)
            {
                case ProviderKind.OpenAI:
                    if (string.IsNullOrWhiteSpace(p.ApiKey)) continue;
                    list.Add(new OpenAICompatibleClient(http, p, "Hermano GPT", "OpenAI", p.PersonalityId));
                    break;
                case ProviderKind.Anthropic:
                    if (string.IsNullOrWhiteSpace(p.ApiKey)) continue;
                    list.Add(new AnthropicClient(http, p, p.PersonalityId));
                    break;
                case ProviderKind.GoogleGemini:
                    if (string.IsNullOrWhiteSpace(p.ApiKey)) continue;
                    list.Add(new GeminiClient(http, p, p.PersonalityId));
                    break;
                case ProviderKind.Grok:
                    if (string.IsNullOrWhiteSpace(p.ApiKey)) continue;
                    list.Add(new OpenAICompatibleClient(http, p, "Hermano Grok", "xAI Grok", p.PersonalityId));
                    break;
                case ProviderKind.Mistral:
                    if (string.IsNullOrWhiteSpace(p.ApiKey)) continue;
                    list.Add(new OpenAICompatibleClient(http, p, "Hermano Mistral", "Mistral", p.PersonalityId));
                    break;
                case ProviderKind.Ollama:
                    foreach (var model in p.OllamaModels.Where(m => !string.IsNullOrWhiteSpace(m)))
                    {
                        var m = model.Trim();
                        var shortLabel = m.Contains(':') ? m.Split(':')[0] : m;
                        if (shortLabel.Length > 24)
                            shortLabel = shortLabel[..21] + "…";
                        var pid = ResolveOllamaPersonality(p, m);
                        list.Add(new OllamaClient(http, p, m, $"Hermano Ollama · {shortLabel}", pid));
                    }

                    break;
            }
        }

        return list;
    }

    private static string ResolveOllamaPersonality(ProviderConfig p, string modelName)
    {
        if (p.OllamaPersonalities.TryGetValue(modelName, out var pid) &&
            !string.IsNullOrWhiteSpace(pid))
            return BrotherPersonalityCatalog.NormalizeId(pid);

        return BrotherPersonalityCatalog.NormalizeId(p.PersonalityId);
    }
}
