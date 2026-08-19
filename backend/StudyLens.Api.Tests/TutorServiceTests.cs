using System.Text.Json;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;
using StudyLens.Api.Models;
using StudyLens.Api.Services;

namespace StudyLens.Api.Tests;

public sealed class TutorServiceTests
{
    [Fact]
    public async Task Explain_UsesRetrievedEvidenceAndReturnsCitations()
    {
        var provider = new FakeTutorAiProvider("Grounded explanation [1].");
        var service = CreateService(provider);

        var response = await service.ExplainAsync(
            "test-course",
            new ExplainRequest("architecture view and viewpoint"),
            CancellationToken.None);

        Assert.Equal("local-test-model", response.Model);
        Assert.Contains("[1]", response.Answer);
        Assert.NotEmpty(response.Citations);
        Assert.Contains("COURSE EVIDENCE", provider.LastPrompt!.UserPrompt);
        Assert.Contains("page 20", provider.LastPrompt.UserPrompt);
    }

    [Fact]
    public async Task Practice_ParsesStructuredQuestions()
    {
        const string json = """
            {"questions":[{"id":"q1","question":"Define an architecture viewpoint.","commandWord":"define","difficulty":"exam","maxScore":4,"sourceNumbers":[1]}]}
            """;
        var service = CreateService(new FakeTutorAiProvider(json));

        var response = await service.CreatePracticeAsync(
            "test-course",
            new PracticeRequest("architecture viewpoint", QuestionCount: 1),
            CancellationToken.None);

        var question = Assert.Single(response.Questions);
        Assert.Equal("q1", question.Id);
        Assert.Equal(4, question.MaxScore);
    }

    [Fact]
    public async Task Grade_ClampsScoreAndReturnsActionableFeedback()
    {
        const string json = """
            {"score":12,"summary":"Good start.","strengths":["Correct definition"],"missingPoints":["Add concerns"],"improvedAnswer":"A viewpoint defines conventions [1]."}
            """;
        var service = CreateService(new FakeTutorAiProvider(json));

        var response = await service.GradeAsync(
            "test-course",
            new GradeRequest("Define an architecture viewpoint.", "It is a convention."),
            CancellationToken.None);

        Assert.Equal(10, response.Score);
        Assert.Contains("Add concerns", response.MissingPoints);
    }

    [Fact]
    public async Task ExplainLecture_UsesOnlySelectedLecturePages()
    {
        var provider = new FakeTutorAiProvider("A structured lecture lesson [1].");
        var service = CreateService(provider, CourseSearchServiceTests.CreateLearningIndex());

        var response = await service.ExplainLectureAsync(
            "test-course",
            new LectureExplainRequest("lecture-4"),
            CancellationToken.None);

        Assert.Equal("L04 Foundations", response.Question);
        Assert.All(response.Citations, citation => Assert.Equal("lecture-4", citation.DocumentId));
        Assert.Contains("LECTURE EVIDENCE", provider.LastPrompt!.UserPrompt);
    }

    [Fact]
    public async Task ExplainExercise_CombinesExerciseAndSolutionEvidence()
    {
        var provider = new FakeTutorAiProvider("Exercise walkthrough [1] [2].");
        var service = CreateService(provider, CourseSearchServiceTests.CreateLearningIndex());

        var response = await service.ExplainExerciseAsync(
            "test-course",
            new ExerciseExplainRequest("exercise-2", "solution-2"),
            CancellationToken.None);

        Assert.Contains(response.Citations, citation => citation.MaterialType == "exercise");
        Assert.Contains(response.Citations, citation => citation.MaterialType == "solution");
        Assert.Contains("EXERCISE AND SOLUTION EVIDENCE", provider.LastPrompt!.UserPrompt);
    }

    private static TutorService CreateService(FakeTutorAiProvider provider, CourseIndex? index = null)
    {
        var search = new CourseSearchService(CourseCatalog.FromCorpora(
            "test-course",
            CourseCorpus.FromIndex(index ?? CourseSearchServiceTests.CreateIndex())));
        return new TutorService(search, new FakeProviderRegistry(provider));
    }

    private sealed class FakeProviderRegistry(FakeTutorAiProvider provider) : ITutorAiProviderRegistry
    {
        public string DefaultProfileId => provider.ProfileId;

        public ITutorAiProvider GetRequired(string? profileId) => provider;

        public async Task<AiProviderCatalog> GetCatalogAsync(CancellationToken cancellationToken) =>
            new(DefaultProfileId, [await provider.GetStatusAsync(cancellationToken)]);
    }

    private sealed class FakeTutorAiProvider(string response) : ITutorAiProvider
    {
        public string ProfileId => "local-test";

        public string DisplayName => "Local test";

        public string Model => "local-test-model";

        public AiTutorPrompt? LastPrompt { get; private set; }

        public Task<AiProviderStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AiProviderStatus(
                ProfileId,
                true,
                "Fake",
                DisplayName,
                Model,
                true,
                "ready"));

        public Task<string> CompleteAsync(AiTutorPrompt prompt, CancellationToken cancellationToken)
        {
            LastPrompt = prompt;
            if (prompt.ResponseSchema is JsonElement schema)
            {
                Assert.Equal(JsonValueKind.Object, schema.ValueKind);
            }

            return Task.FromResult(response);
        }
    }
}
