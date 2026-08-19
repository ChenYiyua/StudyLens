using System.Globalization;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;
using StudyLens.Api.Models;

namespace StudyLens.Api.Services;

public sealed partial class CourseSearchService
{
    private const double K1 = 1.2;
    private const double B = 0.75;
    private const int MaximumExcerptLength = 720;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "can", "define", "describe", "difference", "explain",
        "for", "how", "in", "is", "of", "the", "to", "versus", "what", "why", "with",
    };

    private readonly CourseCatalog catalog;
    private readonly ConcurrentDictionary<string, SearchIndex> indexes;

    public CourseSearchService(CourseCatalog catalog)
    {
        this.catalog = catalog;
        indexes = new ConcurrentDictionary<string, SearchIndex>(
            catalog.Courses.ToDictionary(
                corpus => corpus.CourseId,
                corpus => new SearchIndex(corpus),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    public string DefaultCourseId => catalog.DefaultCourseId;

    public IReadOnlyList<CourseStatusResponse> GetStatuses() => catalog.Courses
        .Select(GetStatus)
        .OrderByDescending(status => status.CourseId.Equals(DefaultCourseId, StringComparison.OrdinalIgnoreCase))
        .ThenBy(status => status.CourseName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool TryGetStatus(string courseId, out CourseStatusResponse status)
    {
        if (!catalog.TryGetCourse(courseId, out var corpus))
        {
            status = null!;
            return false;
        }

        status = GetStatus(corpus);
        return true;
    }

    public bool IsReady(string courseId) =>
        catalog.TryGetCourse(courseId, out var corpus) && corpus.Ready;

    public void AddOrReplace(CourseCorpus corpus)
    {
        catalog.AddOrReplace(corpus);
        indexes[corpus.CourseId] = new SearchIndex(corpus);
    }

    public string? GetError(string courseId) =>
        catalog.TryGetCourse(courseId, out var corpus) ? corpus.Error : "Course not found.";

    public CourseLearningPathResponse GetLearningPath(string courseId)
    {
        if (!catalog.TryGetCourse(courseId, out var corpus) || corpus.Index is null)
        {
            throw new KeyNotFoundException($"Course '{courseId}' was not found or is not ready.");
        }

        var materials = corpus.Index.Documents.Select(ToMaterial).ToArray();
        var lectures = materials
            .Where(material => material.MaterialType == "lecture")
            .OrderBy(material => material.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var exercises = materials.Where(material => material.MaterialType == "exercise").ToArray();
        var solutions = materials.Where(material => material.MaterialType == "solution").ToArray();
        var exerciseKeys = exercises.Select(material => NormalizeExerciseKey(material.Title))
            .Concat(solutions.Select(material => NormalizeExerciseKey(material.Title)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
        var exerciseUnits = exerciseKeys.Select(key =>
        {
            var exercise = exercises.FirstOrDefault(material =>
                NormalizeExerciseKey(material.Title).Equals(key, StringComparison.OrdinalIgnoreCase));
            var solution = solutions.FirstOrDefault(material =>
                NormalizeExerciseKey(material.Title).Equals(key, StringComparison.OrdinalIgnoreCase));
            return new CourseExerciseUnitResponse(
                key,
                exercise?.Title ?? solution?.Title ?? key,
                exercise,
                solution);
        }).ToArray();
        var otherMaterials = materials
            .Where(material => material.MaterialType is not ("lecture" or "exercise" or "solution"))
            .OrderBy(material => material.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CourseLearningPathResponse(courseId, lectures, exerciseUnits, otherMaterials);
    }

    public CourseSearchResponse GetDocumentEvidence(string courseId, string documentId, int limit = 8)
    {
        if (!indexes.TryGetValue(courseId, out var index))
        {
            throw new KeyNotFoundException($"Course '{courseId}' was not found.");
        }

        var document = index.Corpus.Index?.Documents.FirstOrDefault(item =>
            item.Id.Equals(documentId, StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found in course '{courseId}'.");
        }

        var chunks = index.Chunks
            .Where(chunk => chunk.Chunk.DocumentId.Equals(documentId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(chunk => chunk.Chunk.Page)
            .ThenBy(chunk => chunk.Chunk.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var selected = SelectEvenly(chunks, Math.Clamp(limit, 1, 12));
        var results = selected.Select((chunk, position) => new CourseSearchResult(
            chunk.Chunk.Id,
            chunk.Chunk.DocumentId,
            chunk.Chunk.Title,
            chunk.Chunk.RelativePath,
            chunk.Chunk.MaterialType,
            chunk.Chunk.Page,
            CreateExcerpt(chunk.Chunk.Text, []),
            Math.Round(1d - position * 0.001d, 3))).ToArray();
        return new CourseSearchResponse(courseId, document.Title, results.Length, results);
    }

    public CourseSearchResponse Search(string courseId, string query, int limit, string? materialType)
    {
        if (!indexes.TryGetValue(courseId, out var index))
        {
            throw new KeyNotFoundException($"Course '{courseId}' was not found.");
        }

        if (!index.Corpus.Ready)
        {
            throw new InvalidOperationException(index.Corpus.Error);
        }

        var queryTokens = Tokenize(query)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (queryTokens.Length == 0)
        {
            return new CourseSearchResponse(courseId, query, 0, []);
        }

        var normalizedQuery = string.Join(' ', queryTokens);
        var results = index.Chunks
            .Where(chunk => materialType is null ||
                chunk.Chunk.MaterialType.Equals(materialType, StringComparison.OrdinalIgnoreCase))
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = Score(index, chunk, queryTokens, normalizedQuery),
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Chunk.Chunk.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Chunk.Chunk.Page)
            .Take(limit)
            .Select(candidate => new CourseSearchResult(
                candidate.Chunk.Chunk.Id,
                candidate.Chunk.Chunk.DocumentId,
                candidate.Chunk.Chunk.Title,
                candidate.Chunk.Chunk.RelativePath,
                candidate.Chunk.Chunk.MaterialType,
                candidate.Chunk.Chunk.Page,
                CreateExcerpt(candidate.Chunk.Chunk.Text, queryTokens),
                Math.Round(candidate.Score, 3)))
            .ToArray();

        return new CourseSearchResponse(courseId, query, results.Length, results);
    }

    public bool TryGetChunk(
        string courseId,
        string chunkId,
        out CourseChunkDetailResponse detail)
    {
        detail = null!;
        if (!indexes.TryGetValue(courseId, out var index) ||
            !index.ChunksById.TryGetValue(chunkId, out var indexedChunk))
        {
            return false;
        }

        var chunk = indexedChunk.Chunk;
        detail = new CourseChunkDetailResponse(
            courseId,
            chunk.Id,
            chunk.DocumentId,
            chunk.Title,
            chunk.RelativePath,
            chunk.MaterialType,
            chunk.Page,
            chunk.Text);
        return true;
    }

    private static CourseStatusResponse GetStatus(CourseCorpus corpus)
    {
        var index = corpus.Index;
        if (index is null)
        {
            return new CourseStatusResponse(
                corpus.CourseId,
                false,
                false,
                null,
                null,
                0,
                0,
                0,
                0,
                new Dictionary<string, int>(),
                corpus.Error);
        }

        var materialTypes = index.Documents
            .GroupBy(document => document.MaterialType)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        return new CourseStatusResponse(
            corpus.CourseId,
            true,
            corpus.SourceAvailable,
            index.Course.Name,
            index.GeneratedAtUtc,
            index.Statistics.DocumentCount,
            index.Statistics.PageCount,
            index.Statistics.EmptyPageCount,
            index.Statistics.ChunkCount,
            materialTypes,
            null);
    }

    private static CourseMaterialResponse ToMaterial(CourseDocument document) => new(
        document.Id,
        document.Title,
        document.RelativePath,
        document.MaterialType,
        document.PageCount,
        document.ChunkCount);

    private static string NormalizeExerciseKey(string title)
    {
        var withoutRole = Regex.Replace(
            title,
            "(?i)(solution|solutions|answer|answers|exercise|exercises|worksheet)",
            string.Empty);
        var key = Regex.Replace(withoutRole, "[^a-zA-Z0-9]+", "-").Trim('-').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(key) ? title.ToLowerInvariant() : key;
    }

    private static IReadOnlyList<IndexedChunk> SelectEvenly(
        IReadOnlyList<IndexedChunk> chunks,
        int limit)
    {
        if (chunks.Count <= limit)
        {
            return chunks;
        }

        if (limit == 1)
        {
            return [chunks[0]];
        }

        return Enumerable.Range(0, limit)
            .Select(position => (int)Math.Round(position * (chunks.Count - 1d) / (limit - 1d)))
            .Distinct()
            .Select(index => chunks[index])
            .ToArray();
    }

    private static double Score(
        SearchIndex index,
        IndexedChunk chunk,
        IReadOnlyList<string> queryTokens,
        string normalizedQuery)
    {
        var score = chunk.Chunk.MaterialType switch
        {
            "lecture" => 1.2,
            "revision-note" => 0.8,
            "solution" => 0.4,
            _ => 0.0,
        };

        foreach (var token in queryTokens)
        {
            if (!chunk.TokenCounts.TryGetValue(token, out var termFrequency))
            {
                continue;
            }

            var containingChunks = index.DocumentFrequency.GetValueOrDefault(token, 0);
            var inverseDocumentFrequency = Math.Log(
                1 + (index.Chunks.Count - containingChunks + 0.5) / (containingChunks + 0.5));
            var lengthNormalization = termFrequency + K1 *
                (1 - B + B * chunk.TokenCount / index.AverageChunkLength);
            score += inverseDocumentFrequency * (termFrequency * (K1 + 1)) / lengthNormalization;

            if (chunk.Chunk.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.8;
            }
        }

        var normalizedText = string.Join(' ', chunk.Tokens);
        if (queryTokens.Count > 1 && normalizedText.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 2.5;
        }

        return score;
    }

    private static string CreateExcerpt(string text, IReadOnlyList<string> queryTokens)
    {
        if (text.Length <= MaximumExcerptLength)
        {
            return text;
        }

        var firstMatch = queryTokens
            .Select(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();
        var start = Math.Max(0, firstMatch - 180);
        var length = Math.Min(MaximumExcerptLength, text.Length - start);
        var excerpt = text.Substring(start, length).Trim();

        return string.Concat(
            start > 0 ? "…" : string.Empty,
            excerpt,
            start + length < text.Length ? "…" : string.Empty);
    }

    private static IReadOnlyDictionary<string, int> BuildDocumentFrequency(
        IEnumerable<IndexedChunk> chunks)
    {
        var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var chunk in chunks)
        {
            foreach (var token in chunk.TokenCounts.Keys)
            {
                frequencies[token] = frequencies.GetValueOrDefault(token, 0) + 1;
            }
        }

        return frequencies;
    }

    private static IReadOnlyList<string> Tokenize(string value) => WordPattern()
        .Matches(value.ToLower(CultureInfo.InvariantCulture))
        .Select(match => match.Value)
        .ToArray();

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}-]*", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    private sealed class SearchIndex
    {
        public SearchIndex(CourseCorpus corpus)
        {
            Corpus = corpus;
            Chunks = (corpus.Index?.Chunks ?? [])
                .Select(chunk => new IndexedChunk(chunk, Tokenize(chunk.Text)))
                .ToArray();
            ChunksById = Chunks.ToDictionary(
                chunk => chunk.Chunk.Id,
                StringComparer.OrdinalIgnoreCase);
            AverageChunkLength = Chunks.Count == 0 ? 1 : Chunks.Average(chunk => chunk.TokenCount);
            DocumentFrequency = BuildDocumentFrequency(Chunks);
        }

        public CourseCorpus Corpus { get; }

        public IReadOnlyList<IndexedChunk> Chunks { get; }

        public IReadOnlyDictionary<string, IndexedChunk> ChunksById { get; }

        public IReadOnlyDictionary<string, int> DocumentFrequency { get; }

        public double AverageChunkLength { get; }
    }

    private sealed class IndexedChunk
    {
        public IndexedChunk(CourseChunk chunk, IReadOnlyList<string> tokens)
        {
            Chunk = chunk;
            Tokens = tokens;
            TokenCounts = tokens
                .GroupBy(token => token, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        }

        public CourseChunk Chunk { get; }

        public IReadOnlyList<string> Tokens { get; }

        public IReadOnlyDictionary<string, int> TokenCounts { get; }

        public int TokenCount => Tokens.Count;
    }
}
