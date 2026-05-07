using System.Net.Http;
using System.Windows;
using TheIACouncil.Services;

namespace TheIACouncil;

public partial class App : Application
{
    public static HttpClient Http { get; } = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static ConfigService Config { get; } = new();

    public static LLMClientFactory LlmFactory { get; } = new(Http);

    public static CouncilGameService CouncilGame { get; } = new();

    public static OllamaDetector Ollama { get; } = new(Http);

    public static AchievementService Achievements { get; } = new(LlmFactory);
}
