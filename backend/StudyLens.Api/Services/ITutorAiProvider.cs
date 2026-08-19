using System.Text.Json;

namespace StudyLens.Api.Services;

public interface ITutorAiProvider
{
    string ProfileId { get; }

    string DisplayName { get; }

    string Model { get; }

    Task<AiProviderStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task<string> CompleteAsync(AiTutorPrompt prompt, CancellationToken cancellationToken);
}

public sealed record AiTutorPrompt(
    string SystemPrompt,
    string UserPrompt,
    JsonElement? ResponseSchema = null,
    int MaximumOutputTokens = 1600);

public sealed record AiProviderStatus(
    string Id,
    bool Available,
    string Provider,
    string DisplayName,
    string Model,
    bool Local,
    string Message);

public sealed record AiProviderCatalog(
    string DefaultModelId,
    IReadOnlyList<AiProviderStatus> Models);
