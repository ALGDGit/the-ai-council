namespace TheIACouncil.Models;

public sealed class ProviderConfig
{
    public ProviderKind Kind { get; set; }
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "";

    public string PersonalityId { get; set; } = BrotherPersonalityCatalog.DefaultId;

    public Dictionary<string, string> OllamaPersonalities { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> OllamaModels { get; set; } = [];
}
