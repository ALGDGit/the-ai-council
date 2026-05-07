namespace TheIACouncil.Services;

public interface ILLMClient
{
    string BrotherName { get; }
    string ProviderLabel { get; }
    string ModelId { get; }
    string PersonalityId { get; }
    Task<string> CompleteAsync(string userMessage, CancellationToken cancellationToken);
}
