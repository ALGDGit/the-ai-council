namespace TheIACouncil.Models;

public sealed class CouncilTurn
{
    public required string MonkMote { get; init; }
    public required string BrotherName { get; init; }
    public required string PersonalityLabel { get; init; }
    public required string ProviderLabel { get; init; }
    public required string ModelId { get; init; }
    public required string Paragraph { get; init; }
}

public sealed class CouncilVote
{
    public required string MonkMote { get; init; }
    public required string BrotherName { get; init; }
    public required string RawAnswer { get; init; }
    public bool? IsYes { get; init; }

    public string ActaVerdict =>
        IsYes is true ? "SÍ" : IsYes is false ? "NO" : "ambiguo";
}

public sealed class CouncilResult
{
    public required IReadOnlyList<CouncilTurn> Turns { get; init; }
    public required IReadOnlyList<CouncilVote> Votes { get; init; }
    public int YesCount { get; init; }
    public int NoCount { get; init; }
    public int UnclearCount { get; init; }
}
