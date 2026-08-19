using System.Net;
using System.Text;
using System.Text.Json;
using StudyLens.Api.Services;

namespace StudyLens.Api.Tests;

public sealed class OpenAiTutorAiProviderTests
{
    [Fact]
    public async Task CompleteAsync_SendsStatelessStructuredRequestAndReadsOutputText()
    {
        var environmentVariable = $"STUDYLENS_TEST_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(environmentVariable, "test-secret");
        try
        {
            var handler = new RecordingHandler(
                """
                {"output":[{"type":"message","content":[{"type":"output_text","text":"{\"score\":8}"}]}]}
                """);
            var provider = new OpenAiTutorAiProvider(
                new HttpClient(handler),
                CreateProfile(environmentVariable));
            var schema = JsonDocument.Parse(
                """{"type":"object","additionalProperties":false,"properties":{"score":{"type":"integer"}},"required":["score"]}""")
                .RootElement.Clone();

            var result = await provider.CompleteAsync(
                new AiTutorPrompt("System", "User", schema),
                CancellationToken.None);

            Assert.Equal("{\"score\":8}", result);
            Assert.Equal("Bearer", handler.AuthorizationScheme);
            Assert.Equal("test-secret", handler.AuthorizationParameter);
            using var request = JsonDocument.Parse(handler.RequestBody!);
            Assert.False(request.RootElement.GetProperty("store").GetBoolean());
            Assert.Equal("gpt-test", request.RootElement.GetProperty("model").GetString());
            Assert.Equal(
                "json_schema",
                request.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Status_WithoutApiKey_IsUnavailableWithoutCallingProvider()
    {
        var provider = new OpenAiTutorAiProvider(
            new HttpClient(new RecordingHandler("{}")),
            CreateProfile($"MISSING_{Guid.NewGuid():N}"));

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.False(status.Available);
        Assert.False(status.Local);
        Assert.Contains("Set", status.Message);
    }

    private static TutorAiProfileOptions CreateProfile(string environmentVariable) => new()
    {
        Id = "openai-test",
        DisplayName = "OpenAI test",
        Provider = "OpenAI",
        Endpoint = "https://api.openai.test/v1",
        Model = "gpt-test",
        ApiKeyEnvironmentVariable = environmentVariable,
        TimeoutSeconds = 30,
    };

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
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
