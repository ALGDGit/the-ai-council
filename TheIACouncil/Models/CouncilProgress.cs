namespace TheIACouncil.Models;

public readonly record struct CouncilProgress(
    string? ThinkingBrother,
    string? ReasoningLog = null,
    string? VerdictLog = null,
    CouncilTurn? DeliberationTurnFinished = null,
    int? DeliberationSpeakerIndex = null,
    CouncilVote? VoteFinished = null,
    int? VoteSpeakerIndex = null);
