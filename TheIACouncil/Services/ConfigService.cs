using System.IO;
using System.Linq;
using System.Text.Json;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;

    public ConfigService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TheIACouncil");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "config.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded?.Providers is not { Count: > 0 })
                return new AppSettings();

            MergeDefaults(loaded);
            NormalizeConcurrency(loaded);
            NormalizeOllama(loaded);
            NormalizePersonalities(loaded);
            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }

    private static void MergeDefaults(AppSettings loaded)
    {
        var defaults = AppSettings.DefaultProviders();
        foreach (var d in defaults)
        {
            if (loaded.Providers.All(p => p.Kind != d.Kind))
                loaded.Providers.Add(d);
        }
    }

    private static void NormalizeConcurrency(AppSettings loaded)
    {
        if (loaded.MaxConcurrentLlmRequests <= 0)
            loaded.MaxConcurrentLlmRequests = 3;
        else
            loaded.MaxConcurrentLlmRequests = Math.Clamp(loaded.MaxConcurrentLlmRequests, 1, 32);
    }

    private static void NormalizeOllama(AppSettings loaded)
    {
        foreach (var p in loaded.Providers.Where(x => x.Kind == ProviderKind.Ollama))
        {
            p.OllamaModels ??= [];
            if (p.OllamaModels.Count == 0 && !string.IsNullOrWhiteSpace(p.Model))
                p.OllamaModels.Add(p.Model.Trim());
        }
    }

    private static void NormalizePersonalities(AppSettings loaded)
    {
        foreach (var p in loaded.Providers)
        {
            if (string.IsNullOrWhiteSpace(p.PersonalityId))
                p.PersonalityId = BrotherPersonalityCatalog.DefaultId;
            else
                p.PersonalityId = BrotherPersonalityCatalog.NormalizeId(p.PersonalityId);

            p.OllamaPersonalities ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var keys = p.OllamaPersonalities.Keys.ToList();
            foreach (var k in keys)
            {
                var v = p.OllamaPersonalities[k];
                p.OllamaPersonalities[k] = string.IsNullOrWhiteSpace(v)
                    ? BrotherPersonalityCatalog.DefaultId
                    : BrotherPersonalityCatalog.NormalizeId(v);
            }
        }
    }
}
