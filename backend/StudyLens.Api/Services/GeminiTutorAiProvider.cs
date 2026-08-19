using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudyLens.Api.Services;

public sealed class GeminiTutorAiProvider : ITutorAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient client;
    private readonly TutorAiProfileOptions options;
    private readonly string? apiKey;

    public GeminiTutorAiProvider(HttpClient client, TutorAiProfileOptions options)
    {
        this.client = client;
        this.options = options;
        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "https://generativelanguage.googleapis.com/v1beta"
            : options.Endpoint;
        this.client.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
        this.client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 30, 900));
        apiKey = string.IsNullOrWhiteSpace(options.ApiKeyEnvironmentVariable)
            ? null
            : Environment.GetEnvironmentVariable(options.ApiKeyEnvironmentVariable);
    }

    public string ProfileId => options.Id;

    public string DisplayName => options.DisplayName;

    public string Model => options.Model;

    public Task<AiProviderStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var available = !string.IsNullOrWhiteSpace(apiKey);
        return Task.FromResult(new AiProviderStatus(
            ProfileId,
            available,
            "Google Gemini",
            DisplayName,
            Model,
            false,
            available
                ? "Gemini API key is configured. Free-tier limits and data terms apply."
                : $"Set {options.ApiKeyEnvironmentVariable ?? "GEMINI_API_KEY"} to enable the Gemini free tier."));
    }

    public async Task<string> CompleteAsync(
        AiTutorPrompt prompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"{DisplayName} is not configured. Set {options.ApiKeyEnvironmentVariable ?? "GEMINI_API_KEY"} first.");
        }

        var generationConfig = new JsonObject
        {
            ["maxOutputTokens"] = Math.Clamp(prompt.MaximumOutputTokens, 200, 4000),
        };
        if (prompt.ResponseSchema is JsonElement schema)
        {
            generationConfig["responseMimeType"] = "application/json";
            generationConfig["responseJsonSchema"] = JsonNode.Parse(schema.GetRawText());
        }

        var request = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt.SystemPrompt }),
            },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt.UserPrompt }),
            }),
            ["generationConfig"] = generationConfig,
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"models/{Uri.EscapeDataString(Model)}:generateContent")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Add("x-goog-api-key", apiKey);
        using var response = await client.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini API returned {(int)response.StatusCode}: {ReadApiError(responseText)}");
        }

        using var document = JsonDocument.Parse(responseText);
        var output = ExtractOutputText(document.RootElement);
        return string.IsNullOrWhiteSpace(output)
            ? throw new InvalidOperationException("The Gemini model returned no text output.")
            : output.Trim();
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var textParts = parts.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text));
            var text = string.Join(Environment.NewLine, textParts);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static string ReadApiError(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            return document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString() ?? "Unknown API error.";
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return "The provider returned an unreadable error response.";
        }
    }
}
