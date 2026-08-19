using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyLens.Api.Services;

public sealed class OllamaTutorAiProvider : ITutorAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient client;
    private readonly TutorAiProfileOptions options;

    public OllamaTutorAiProvider(HttpClient client, TutorAiProfileOptions options)
    {
        this.client = client;
        this.options = options;
        this.client.BaseAddress = new Uri(this.options.Endpoint.TrimEnd('/') + "/");
        this.client.Timeout = TimeSpan.FromSeconds(Math.Clamp(this.options.TimeoutSeconds, 30, 900));
    }

    public string ProfileId => options.Id;

    public string DisplayName => options.DisplayName;

    public string Model => options.Model;

    public async Task<AiProviderStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetFromJsonAsync<OllamaTagsResponse>("api/tags", JsonOptions, cancellationToken);
            var installed = response?.Models.Any(model =>
                model.Name.Equals(Model, StringComparison.OrdinalIgnoreCase) ||
                model.Model.Equals(Model, StringComparison.OrdinalIgnoreCase)) == true;

            return installed
                ? new AiProviderStatus(
                    ProfileId,
                    true,
                    "Ollama",
                    DisplayName,
                    Model,
                    true,
                    "Local model is ready.")
                : new AiProviderStatus(
                    ProfileId,
                    false,
                    "Ollama",
                    DisplayName,
                    Model,
                    true,
                    $"Ollama is running, but model '{Model}' is not installed.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new AiProviderStatus(
                ProfileId,
                false,
                "Ollama",
                DisplayName,
                Model,
                true,
                "Ollama is not reachable. Run scripts/setup-local-ai.ps1.");
        }
    }

    public async Task<string> CompleteAsync(AiTutorPrompt prompt, CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
            Model,
            [
                new OllamaMessage("system", prompt.SystemPrompt),
                new OllamaMessage("user", prompt.UserPrompt),
            ],
            false,
            false,
            prompt.ResponseSchema,
            new OllamaGenerationOptions(
                0.1,
                Math.Clamp(options.ContextLength, 4096, 32768),
                Math.Clamp(prompt.MaximumOutputTokens, 200, 4000)));

        using var response = await client.PostAsJsonAsync("api/chat", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.Message.Content))
        {
            throw new InvalidOperationException("The local model returned an empty response.");
        }

        return payload.Message.Content.Trim();
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaMessage> Messages,
        bool Stream,
        bool Think,
        JsonElement? Format,
        OllamaGenerationOptions Options);

    private sealed record OllamaGenerationOptions(
        double Temperature,
        [property: JsonPropertyName("num_ctx")] int ContextLength,
        [property: JsonPropertyName("num_predict")] int MaximumOutputTokens);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaMessage Message);

    private sealed record OllamaTagsResponse(IReadOnlyList<OllamaModel> Models);

    private sealed record OllamaModel(string Name, string Model);
}
