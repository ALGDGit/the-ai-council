namespace TheIACouncil.Models;

public sealed class AppSettings
{
    /// <summary>
    /// Máximo de llamadas al modelo en paralelo (votación del concilio, impostor, etc.).
    /// Valores bajos (2–4) evitan errores CUDA/OOM con Ollama local y muchas IAs.
    /// </summary>
    public int MaxConcurrentLlmRequests { get; set; } = 3;

    public List<ProviderConfig> Providers { get; set; } = DefaultProviders();

    public static List<ProviderConfig> DefaultProviders() =>
    [
        new()
        {
            Kind = ProviderKind.OpenAI,
            Enabled = false,
            Model = "gpt-4o-mini",
            BaseUrl = "https://api.openai.com/v1"
        },
        new()
        {
            Kind = ProviderKind.Anthropic,
            Enabled = false,
            Model = "claude-3-5-haiku-20241022",
            BaseUrl = "https://api.anthropic.com"
        },
        new()
        {
            Kind = ProviderKind.GoogleGemini,
            Enabled = false,
            Model = "gemini-2.0-flash",
            BaseUrl = ""
        },
        new()
        {
            Kind = ProviderKind.Grok,
            Enabled = false,
            Model = "grok-2-latest",
            BaseUrl = "https://api.x.ai/v1"
        },
        new()
        {
            Kind = ProviderKind.Mistral,
            Enabled = false,
            Model = "mistral-small-latest",
            BaseUrl = "https://api.mistral.ai/v1"
        },
        new()
        {
            Kind = ProviderKind.Ollama,
            Enabled = false,
            Model = "",
            BaseUrl = "http://localhost:11434",
            OllamaModels = []
        }
    ];
}
