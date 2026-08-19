using Microsoft.Extensions.Options;

namespace StudyLens.Api.Services;

public interface ITutorAiProviderRegistry
{
    string DefaultProfileId { get; }

    ITutorAiProvider GetRequired(string? profileId);

    Task<AiProviderCatalog> GetCatalogAsync(CancellationToken cancellationToken);
}

public sealed class TutorAiProviderRegistry : ITutorAiProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ITutorAiProvider> providers;

    public TutorAiProviderRegistry(
        IHttpClientFactory httpClientFactory,
        IOptions<TutorAiOptions> options)
    {
        var settings = options.Value;
        providers = settings.Profiles
            .Select(profile => ApplyLegacyLocalSettings(profile, settings))
            .Select(profile => CreateProvider(httpClientFactory.CreateClient(), profile))
            .ToDictionary(provider => provider.ProfileId, StringComparer.OrdinalIgnoreCase);
        if (providers.Count == 0)
        {
            var fallback = new TutorAiProfileOptions
            {
                Id = "ollama-local",
                DisplayName = "Qwen local",
                Provider = "Ollama",
                Endpoint = settings.Endpoint,
                Model = settings.Model,
                TimeoutSeconds = settings.TimeoutSeconds,
                ContextLength = settings.ContextLength,
            };
            var provider = CreateProvider(httpClientFactory.CreateClient(), fallback);
            providers = new Dictionary<string, ITutorAiProvider>(StringComparer.OrdinalIgnoreCase)
            {
                [provider.ProfileId] = provider,
            };
        }

        DefaultProfileId = providers.ContainsKey(settings.DefaultProfileId)
            ? settings.DefaultProfileId
            : providers.Keys.First();
    }

    public string DefaultProfileId { get; }

    public ITutorAiProvider GetRequired(string? profileId)
    {
        var selectedId = string.IsNullOrWhiteSpace(profileId) ? DefaultProfileId : profileId;
        return providers.TryGetValue(selectedId, out var provider)
            ? provider
            : throw new InvalidOperationException($"AI model profile '{selectedId}' is not configured.");
    }

    public async Task<AiProviderCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var statuses = await Task.WhenAll(providers.Values.Select(
            provider => provider.GetStatusAsync(cancellationToken)));
        return new AiProviderCatalog(
            DefaultProfileId,
            statuses
                .OrderByDescending(status => status.Id.Equals(DefaultProfileId, StringComparison.OrdinalIgnoreCase))
                .ThenBy(status => status.Local ? 0 : 1)
                .ThenBy(status => status.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static TutorAiProfileOptions ApplyLegacyLocalSettings(
        TutorAiProfileOptions profile,
        TutorAiOptions settings) =>
        profile.Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) &&
        profile.Id.Equals("ollama-local", StringComparison.OrdinalIgnoreCase)
            ? new TutorAiProfileOptions
            {
                Id = profile.Id,
                DisplayName = profile.DisplayName,
                Provider = profile.Provider,
                Endpoint = settings.Endpoint,
                Model = settings.Model,
                TimeoutSeconds = settings.TimeoutSeconds,
                ContextLength = settings.ContextLength,
            }
            : profile;

    private static ITutorAiProvider CreateProvider(
        HttpClient client,
        TutorAiProfileOptions profile) => profile.Provider.ToLowerInvariant() switch
        {
            "ollama" => new OllamaTutorAiProvider(client, profile),
            "openai" => new OpenAiTutorAiProvider(client, profile),
            "gemini" => new GeminiTutorAiProvider(client, profile),
            _ => throw new InvalidOperationException(
                $"Unsupported AI provider '{profile.Provider}' for profile '{profile.Id}'."),
        };
}
