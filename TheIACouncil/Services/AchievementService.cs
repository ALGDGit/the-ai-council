using System.IO;
using System.Text.Json;
using TheIACouncil.Models;

namespace TheIACouncil.Services;

public sealed class AchievementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly LLMClientFactory _llm;
    private readonly string _path;
    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);

    public AchievementService(LLMClientFactory llm)
    {
        _llm = llm;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TheIACouncil");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "achievements.json");
        Load();
    }

    public void Load()
    {
        _unlocked.Clear();
        if (!File.Exists(_path))
            return;
        try
        {
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<AchievementStoreData>(json, JsonOptions);
            if (data?.Unlocked is null)
                return;
            foreach (var id in data.Unlocked)
                _unlocked.Add(id);
        }
        catch
        {
            // ignorar archivo corrupto
        }
    }

    public IReadOnlySet<string> Unlocked => _unlocked;

    public bool IsUnlocked(string id) => _unlocked.Contains(id);

    private void Save()
    {
        var data = new AchievementStoreData { Unlocked = _unlocked.ToList() };
        File.WriteAllText(_path, JsonSerializer.Serialize(data, JsonOptions));
    }

    public void Unlock(string id)
    {
        if (!_unlocked.Add(id))
            return;
        Save();
    }

    /// <summary>Tras guardar configuración: al menos una IA usable.</summary>
    public void EvaluateAfterConfigSave(AppSettings settings)
    {
        if (_llm.CreateEnabledClients(settings).Count >= 1)
            Unlock(AchievementIds.FirstSteps);
    }

    /// <summary>Tras una ronda del consejo terminada con éxito.</summary>
    public void EvaluateAfterCouncil(CouncilResult result)
    {
        var n = result.Votes.Count;
        if (n == 0)
            return;

        Unlock(AchievementIds.CouncilSpoke);

        if (n < 5)
            return;

        if (result.YesCount == n && result.NoCount == 0 && result.UnclearCount == 0)
            Unlock(AchievementIds.AbsoluteMajority);

        if (result.NoCount == n && result.YesCount == 0 && result.UnclearCount == 0)
            Unlock(AchievementIds.Sentenced);

        if (result.UnclearCount == n && result.YesCount == 0 && result.NoCount == 0)
            Unlock(AchievementIds.ArtificialStupidity);
    }
}
