using System.Linq;

namespace TheIACouncil.Models;

public sealed class BrotherPersonalityItem
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string PromptInstruction { get; init; }
}

public static class BrotherPersonalityCatalog
{
    /// <summary>Monje — personalidad por defecto del consejo.</summary>
    public const string DefaultId = "monk";

    public static IReadOnlyList<BrotherPersonalityItem> All { get; } =
    [
        new BrotherPersonalityItem
        {
            Id = "monk",
            DisplayName = "Monje",
            PromptInstruction =
                "Recuerda que eres un monje asceta del consejo: voz sobria, pausa contemplativa y un humor muy seco si encaja con el tema."
        },
        new BrotherPersonalityItem
        {
            Id = "knight",
            DisplayName = "Caballero",
            PromptInstruction =
                "Recuerda que eres un caballero en misión sagrada: cortesía, honor, frases un tanto épicas y un dejo caballeresco que no aburre."
        },
        new BrotherPersonalityItem
        {
            Id = "commoner",
            DisplayName = "Plebeyo",
            PromptInstruction =
                "Habla como un plebeyo del mercado: lenguaje llano, directo, con sal y picardía popular."
        },
        new BrotherPersonalityItem
        {
            Id = "pirate",
            DisplayName = "Pirata",
            PromptInstruction =
                "Habla como si fueras un pirata en el consejo: jerga de cubierta con moderación, humor marinero y sin perder el rumbo del debate."
        },
        new BrotherPersonalityItem
        {
            Id = "chiquito",
            DisplayName = "Chiquito de la calzada",
            PromptInstruction =
                "Imita el tono cómico del Chiquito de la Calzada: pausas, torpezas graciosas y frases memorables; respeta el fondo de la pregunta y no insultes a nadie."
        }
    ];

    private static readonly Dictionary<string, BrotherPersonalityItem> ById = All.ToDictionary(
        x => x.Id,
        StringComparer.OrdinalIgnoreCase);

    public static string NormalizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return DefaultId;
        return ById.ContainsKey(id.Trim()) ? id.Trim() : DefaultId;
    }

    public static BrotherPersonalityItem Get(string? id) =>
        ById.TryGetValue(NormalizeId(id), out var item) ? item : ById[DefaultId];

    public static string GetDisplayName(string? id) => Get(id).DisplayName;

    public static string GetPromptInstruction(string? id) => Get(id).PromptInstruction;
}
