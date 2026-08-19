using System.Net;
using System.Text;
using System.Text.Json;
using StudyLens.Api.Services;

namespace StudyLens.Api.Tests;

public sealed class GeminiTutorAiProviderTests
{
    [Fact]
    public async Task CompleteAsync_SendsStructuredRequestAndReadsCandidateText()
    {
        var environmentVariable = $"STUDYLENS_GEMINI_TEST_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(environmentVariable, "gemini-test-secret");
        try
        {
            var handler = new RecordingHandler(
                """
                {"candidates":[{"content":{"parts":[{"text":"{\"score\":9}"}]}}]}
                """);
            var provider = new GeminiTutorAiProvider(
                new HttpClient(handler),
                CreateProfile(environmentVariable));
            var schema = JsonDocument.Parse(
                """{"type":"object","additionalProperties":false,"properties":{"score":{"type":"integer"}},"required":["score"]}""")
                .RootElement.Clone();

            var result = await provider.CompleteAsync(
                new AiTutorPrompt("System", "User", schema),
                CancellationToken.None);

            Assert.Equal("{\"score\":9}", result);
            Assert.Equal("gemini-test-secret", handler.ApiKey);
            Assert.EndsWith("models/gemini-test:generateContent", handler.RequestPath);
            using var request = JsonDocument.Parse(handler.RequestBody!);
            var config = request.RootElement.GetProperty("generationConfig");
            Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
            Assert.Equal("object", config.GetProperty("responseJsonSchema").GetProperty("type").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Status_WithoutApiKey_IsUnavailableWithoutCallingProvider()
    {
        var provider = new GeminiTutorAiProvider(
            new HttpClient(new RecordingHandler("{}")),
            CreateProfile($"MISSING_{Guid.NewGuid():N}"));

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.False(status.Available);
        Assert.False(status.Local);
        Assert.Contains("GEMINI", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TutorAiProfileOptions CreateProfile(string environmentVariable) => new()
    {
        Id = "gemini-test",
        DisplayName = "Gemini test",
        Provider = "Gemini",
        Endpoint = "https://generativelanguage.googleapis.test/v1beta",
        Model = "gemini-test",
        ApiKeyEnvironmentVariable = environmentVariable,
        TimeoutSeconds = 30,
    };

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? ApiKey { get; private set; }

        public string? RequestPath { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ApiKey = request.Headers.GetValues("x-goog-api-key").SingleOrDefault();
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
