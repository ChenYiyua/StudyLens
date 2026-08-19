namespace StudyLens.Api.Services;

public sealed class TutorAiOptions
{
    public const string SectionName = "TutorAi";

    public string DefaultProfileId { get; init; } = "ollama-local";

    public string Endpoint { get; init; } = "http://127.0.0.1:11434";

    public string Model { get; init; } = "qwen3.5:4b";

    public int TimeoutSeconds { get; init; } = 300;

    public int ContextLength { get; init; } = 4096;

    public IReadOnlyList<TutorAiProfileOptions> Profiles { get; init; } = [];
}

public sealed class TutorAiProfileOptions
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string? ApiKeyEnvironmentVariable { get; init; }

    public int TimeoutSeconds { get; init; } = 300;

    public int ContextLength { get; init; } = 4096;
}
