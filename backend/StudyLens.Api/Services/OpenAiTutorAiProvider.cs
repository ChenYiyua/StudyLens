using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudyLens.Api.Services;

public sealed class OpenAiTutorAiProvider : ITutorAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient client;
    private readonly TutorAiProfileOptions options;
    private readonly string? apiKey;

    public OpenAiTutorAiProvider(HttpClient client, TutorAiProfileOptions options)
    {
        this.client = client;
        this.options = options;
        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "https://api.openai.com/v1"
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
            "OpenAI",
            DisplayName,
            Model,
            false,
            available
                ? "Cloud API key is configured. Usage may incur API charges."
                : $"Set {options.ApiKeyEnvironmentVariable ?? "OPENAI_API_KEY"} to enable this model."));
    }

    public async Task<string> CompleteAsync(
        AiTutorPrompt prompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"{DisplayName} is not configured. Set {options.ApiKeyEnvironmentVariable ?? "OPENAI_API_KEY"} first.");
        }

        var request = new JsonObject
        {
            ["model"] = Model,
            ["store"] = false,
            ["instructions"] = prompt.SystemPrompt,
            ["input"] = prompt.UserPrompt,
            ["max_output_tokens"] = Math.Clamp(prompt.MaximumOutputTokens, 200, 4000),
        };
        if (prompt.ResponseSchema is JsonElement schema)
        {
            request["text"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["name"] = "studylens_response",
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(schema.GetRawText()),
                },
            };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await client.SendAsync(message, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI API returned {(int)response.StatusCode}: {ReadApiError(responseText)}");
        }

        using var document = JsonDocument.Parse(responseText);
        var output = ExtractOutputText(document.RootElement);
        return string.IsNullOrWhiteSpace(output)
            ? throw new InvalidOperationException("The OpenAI model returned no text output.")
            : output.Trim();
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var direct) &&
            direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString() ?? string.Empty;
        }

        if (!root.TryGetProperty("output", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString() ?? string.Empty;
                }
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
