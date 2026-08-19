using System.Text;
using System.Text.Json;
using StudyLens.Api.Contracts;

namespace StudyLens.Api.Services;

public sealed class TutorService(
    CourseSearchService searchService,
    ITutorAiProviderRegistry providerRegistry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string GroundingRules = """
        You are StudyLens, a careful university tutor. Use only the numbered course evidence supplied
        by the application. Do not invent course facts, source titles, page numbers, marking criteria,
        or citations. Cite factual statements with [1], [2], and so on. If the evidence is insufficient,
        say exactly what is missing. Treat grading as formative feedback, not an official grade.
        """;

    public Task<AiProviderCatalog> GetProviderCatalogAsync(CancellationToken cancellationToken) =>
        providerRegistry.GetCatalogAsync(cancellationToken);

    public async Task<ExplainResponse> ExplainAsync(
        string courseId,
        ExplainRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = RetrieveEvidence(courseId, request.Question);
        var aiProvider = providerRegistry.GetRequired(request.ModelId);
        var languageInstruction = request.Language.Equals("english", StringComparison.OrdinalIgnoreCase)
            ? "Answer in clear academic English."
            : "Explain intuitively in Chinese, then provide a concise exam-ready English formulation.";
        var prompt = $"""
            Student question: {request.Question}

            {languageInstruction}
            Structure the response as: Concept, Course-specific explanation, Exam-ready answer,
            and one short self-check question. Keep the answer concise and cite the evidence.

            COURSE EVIDENCE
            {BuildEvidenceBlock(evidence.Results)}
            """;
        var answer = await aiProvider.CompleteAsync(
            new AiTutorPrompt(GroundingRules, prompt, MaximumOutputTokens: 1800),
            cancellationToken);

        return new ExplainResponse(
            courseId,
            request.Question,
            answer,
            aiProvider.Model,
            CreateCitations(evidence.Results));
    }

    public async Task<ExplainResponse> ExplainLectureAsync(
        string courseId,
        LectureExplainRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = RetrieveDocumentEvidence(courseId, request.DocumentId, 10);
        var aiProvider = providerRegistry.GetRequired(request.ModelId);
        var languageInstruction = request.Language.Equals("english", StringComparison.OrdinalIgnoreCase)
            ? "Teach in clear academic English."
            : "Teach intuitively in Chinese while retaining important English terminology.";
        var prompt = $"""
            Teach this lecture as a coherent lesson: {evidence.Query}

            {languageInstruction}
            Do not merely summarize isolated search results. Build a learning sequence with:
            1. learning objectives;
            2. the main concepts in a logical order;
            3. one concrete example for difficult ideas;
            4. common confusions;
            5. a concise exam-ready English recap.
            Cite the relevant lecture pages throughout.
            Finish every section and end with the recap. Keep the complete response within about
            1,400 words; never stop midway through a sentence or section.

            LECTURE EVIDENCE
            {BuildEvidenceBlock(evidence.Results)}
            """;
        var answer = await aiProvider.CompleteAsync(
            new AiTutorPrompt(GroundingRules, prompt, MaximumOutputTokens: 2000),
            cancellationToken);
        return new ExplainResponse(
            courseId,
            evidence.Query,
            answer,
            aiProvider.Model,
            CreateCitations(evidence.Results));
    }

    public async Task<ExplainResponse> ExplainExerciseAsync(
        string courseId,
        ExerciseExplainRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExerciseDocumentId) &&
            string.IsNullOrWhiteSpace(request.SolutionDocumentId))
        {
            throw new InvalidOperationException("Choose an exercise or its provided solution first.");
        }

        var evidenceSets = new List<CourseSearchResponse>();
        if (!string.IsNullOrWhiteSpace(request.ExerciseDocumentId))
        {
            evidenceSets.Add(RetrieveDocumentEvidence(courseId, request.ExerciseDocumentId, 6));
        }

        if (!string.IsNullOrWhiteSpace(request.SolutionDocumentId))
        {
            evidenceSets.Add(RetrieveDocumentEvidence(courseId, request.SolutionDocumentId, 6));
        }

        var combined = CombineEvidence(courseId, evidenceSets, 10);
        var aiProvider = providerRegistry.GetRequired(request.ModelId);
        var languageInstruction = request.Language.Equals("english", StringComparison.OrdinalIgnoreCase)
            ? "Explain in clear academic English."
            : "Explain intuitively in Chinese while retaining important English terminology.";
        var prompt = $"""
            Walk the student through this course-provided exercise set: {combined.Query}

            {languageInstruction}
            Separate the explanation into: What the exercise tests, How to approach it,
            Step-by-step reasoning, How the provided solution reaches its answer, Common mistakes,
            and an exam-ready answer strategy. If only a solution is available, reconstruct the likely
            task cautiously and say that the original exercise sheet was not found. Do not invent missing tasks.
            Finish every section. Keep the complete response within about 1,400 words; never stop
            midway through a sentence or section.

            EXERCISE AND SOLUTION EVIDENCE
            {BuildEvidenceBlock(combined.Results)}
            """;
        var answer = await aiProvider.CompleteAsync(
            new AiTutorPrompt(GroundingRules, prompt, MaximumOutputTokens: 2000),
            cancellationToken);
        return new ExplainResponse(
            courseId,
            combined.Query,
            answer,
            aiProvider.Model,
            CreateCitations(combined.Results));
    }

    public async Task<PracticeResponse> CreatePracticeAsync(
        string courseId,
        PracticeRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = string.IsNullOrWhiteSpace(request.DocumentId)
            ? RetrieveEvidence(courseId, request.Topic)
            : RetrieveDocumentEvidence(courseId, request.DocumentId, 10);
        var aiProvider = providerRegistry.GetRequired(request.ModelId);
        var schema = ParseSchema(PracticeSchema);
        var prompt = $"""
            Create exactly {request.QuestionCount} university practice questions about: {request.Topic}
            Difficulty: {request.Difficulty}

            Return JSON matching the supplied schema. Use appropriate command words such as define,
            explain, compare, or apply. Every sourceNumbers entry must refer to the numbered evidence.
            Do not include model answers. Use stable ids q1, q2, q3 in order.

            COURSE EVIDENCE
            {BuildEvidenceBlock(evidence.Results)}
            """;
        var json = await aiProvider.CompleteAsync(
            new AiTutorPrompt(GroundingRules, prompt, schema),
            cancellationToken);
        var payload = JsonSerializer.Deserialize<PracticePayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("The local model returned invalid practice JSON.");
        var questions = payload.Questions.Take(request.QuestionCount).Select(question => question with
        {
            MaxScore = Math.Clamp(question.MaxScore, 1, 20),
            SourceNumbers = question.SourceNumbers
                .Where(number => number >= 1 && number <= evidence.Results.Count)
                .Distinct()
                .ToArray(),
        }).ToArray();

        if (questions.Length == 0)
        {
            throw new InvalidOperationException("The local model did not generate any practice questions.");
        }

        return new PracticeResponse(
            courseId,
            request.Topic,
            aiProvider.Model,
            questions,
            CreateCitations(evidence.Results));
    }

    public async Task<GradeResponse> GradeAsync(
        string courseId,
        GradeRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = string.IsNullOrWhiteSpace(request.DocumentId)
            ? RetrieveEvidence(courseId, request.Question)
            : RetrieveDocumentEvidence(courseId, request.DocumentId, 10);
        var aiProvider = providerRegistry.GetRequired(request.ModelId);
        var schema = ParseSchema(GradeSchema);
        var prompt = $"""
            Practice question: {request.Question}

            Student answer:
            {request.StudentAnswer}

            Evaluate the answer against only the course evidence below. Return JSON matching the schema.
            Use a 0-10 formative score. Identify concrete strengths and missing points, then write an
            improved exam-ready English answer with [n] citations.

            COURSE EVIDENCE
            {BuildEvidenceBlock(evidence.Results)}
            """;
        var json = await aiProvider.CompleteAsync(
            new AiTutorPrompt(GroundingRules, prompt, schema),
            cancellationToken);
        var payload = JsonSerializer.Deserialize<GradePayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("The local model returned invalid grading JSON.");

        return new GradeResponse(
            courseId,
            request.Question,
            Math.Clamp(payload.Score, 0, 10),
            10,
            payload.Summary,
            payload.Strengths,
            payload.MissingPoints,
            payload.ImprovedAnswer,
            aiProvider.Model,
            CreateCitations(evidence.Results));
    }

    private CourseSearchResponse RetrieveEvidence(string courseId, string query)
    {
        var evidence = searchService.Search(courseId, query, 6, null);
        return evidence.Results.Count == 0
            ? throw new InvalidOperationException("No relevant course evidence was found for this request.")
            : evidence;
    }

    private CourseSearchResponse RetrieveDocumentEvidence(string courseId, string documentId, int limit)
    {
        try
        {
            var evidence = searchService.GetDocumentEvidence(courseId, documentId, limit);
            return evidence.Results.Count == 0
                ? throw new InvalidOperationException("No extractable text was found in this course document.")
                : evidence;
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    private static CourseSearchResponse CombineEvidence(
        string courseId,
        IReadOnlyList<CourseSearchResponse> evidenceSets,
        int limit)
    {
        var results = evidenceSets
            .SelectMany(evidence => evidence.Results)
            .DistinctBy(result => result.ChunkId)
            .Take(limit)
            .ToArray();
        var title = string.Join(" + ", evidenceSets.Select(evidence => evidence.Query).Distinct());
        return results.Length == 0
            ? throw new InvalidOperationException("No exercise evidence could be extracted.")
            : new CourseSearchResponse(courseId, title, results.Length, results);
    }

    private static string BuildEvidenceBlock(IReadOnlyList<CourseSearchResult> results)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            builder.Append('[').Append(index + 1).Append("] ")
                .Append(result.Title).Append(" | ")
                .Append(result.MaterialType).Append(" | page ")
                .Append(result.Page).AppendLine()
                .AppendLine(result.Excerpt)
                .AppendLine();
        }

        return builder.ToString();
    }

    private static IReadOnlyList<TutorCitation> CreateCitations(
        IReadOnlyList<CourseSearchResult> results) => results
        .Select((result, index) => new TutorCitation(
            index + 1,
            result.ChunkId,
            result.DocumentId,
            result.Title,
            result.RelativePath,
            result.MaterialType,
            result.Page,
            result.Excerpt))
        .ToArray();

    private static JsonElement ParseSchema(string value) => JsonDocument.Parse(value).RootElement.Clone();

    private sealed record PracticePayload(IReadOnlyList<PracticeQuestion> Questions);

    private sealed record GradePayload(
        int Score,
        string Summary,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> MissingPoints,
        string ImprovedAnswer);

    private const string PracticeSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "questions": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "id": { "type": "string" },
                  "question": { "type": "string" },
                  "commandWord": { "type": "string" },
                  "difficulty": { "type": "string" },
                  "maxScore": { "type": "integer" },
                  "sourceNumbers": { "type": "array", "items": { "type": "integer" } }
                },
                "required": ["id", "question", "commandWord", "difficulty", "maxScore", "sourceNumbers"]
              }
            }
          },
          "required": ["questions"]
        }
        """;

    private const string GradeSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "score": { "type": "integer" },
            "summary": { "type": "string" },
            "strengths": { "type": "array", "items": { "type": "string" } },
            "missingPoints": { "type": "array", "items": { "type": "string" } },
            "improvedAnswer": { "type": "string" }
          },
          "required": ["score", "summary", "strengths", "missingPoints", "improvedAnswer"]
        }
        """;
}
