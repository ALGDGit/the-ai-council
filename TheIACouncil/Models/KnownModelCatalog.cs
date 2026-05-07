namespace TheIACouncil.Models;

/// <summary>
/// Modelos habituales por proveedor en la nube. Ollama no usa esta lista: los modelos vienen del daemon local.
/// </summary>
public static class KnownModelCatalog
{
    private static readonly string[] OpenAI =
    [
        "gpt-4o-mini",
        "gpt-4o",
        "gpt-4-turbo",
        "gpt-4-turbo-preview",
        "gpt-4.1",
        "gpt-4.1-mini",
        "o1-mini",
        "o1",
        "o3-mini"
    ];

    private static readonly string[] Anthropic =
    [
        "claude-3-5-haiku-20241022",
        "claude-3-5-sonnet-20241022",
        "claude-3-opus-20240229",
        "claude-3-haiku-20240307"
    ];

    private static readonly string[] Gemini =
    [
        "gemini-2.0-flash",
        "gemini-2.0-flash-lite",
        "gemini-1.5-pro",
        "gemini-1.5-flash",
        "gemini-1.5-flash-8b"
    ];

    private static readonly string[] Grok =
    [
        "grok-2-latest",
        "grok-2-vision-latest",
        "grok-beta"
    ];

    private static readonly string[] Mistral =
    [
        "mistral-small-latest",
        "mistral-medium-latest",
        "mistral-large-latest",
        "pixtral-12b-2409",
        "open-mistral-7b"
    ];

    public static IReadOnlyList<string> For(ProviderKind kind) =>
        kind switch
        {
            ProviderKind.OpenAI => OpenAI,
            ProviderKind.Anthropic => Anthropic,
            ProviderKind.GoogleGemini => Gemini,
            ProviderKind.Grok => Grok,
            ProviderKind.Mistral => Mistral,
            _ => []
        };
}
