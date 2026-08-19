using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public sealed class CourseCorpus
{
    private static readonly HashSet<string> SupportedDocumentExtensions = new(
        [".pdf", ".md", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    private CourseCorpus(string courseId, CourseIndex? index, string? sourceRoot, string? error)
    {
        CourseId = courseId;
        Index = index;
        SourceRoot = sourceRoot;
        Error = error;
    }

    public string CourseId { get; }

    public CourseIndex? Index { get; }

    public string? Error { get; }

    public string? SourceRoot { get; }

    public bool Ready => Index is not null;

    public bool SourceAvailable => SourceRoot is not null && Directory.Exists(SourceRoot);

    public static CourseCorpus FromIndex(CourseIndex index, string? sourceRoot = null) =>
        new(index.Course.Id, index, NormalizeSourceRoot(sourceRoot), null);

    public static CourseCorpus Load(IWebHostEnvironment environment, CourseSourceOptions options)
    {
        var configuredPath = options.IndexPath;
        var indexPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);

        if (!File.Exists(indexPath))
        {
            return new CourseCorpus(
                options.Id,
                null,
                null,
                $"Course index not found for '{options.Id}'. Run scripts/setup-course.ps1 first.");
        }

        try
        {
            var json = File.ReadAllText(indexPath);
            var index = JsonSerializer.Deserialize<CourseIndex>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (index is null)
            {
                return new CourseCorpus(options.Id, null, null, "The course index is empty or invalid.");
            }

            if (!index.Course.Id.Equals(options.Id, StringComparison.OrdinalIgnoreCase))
            {
                return new CourseCorpus(
                    options.Id,
                    null,
                    null,
                    $"Configured course id '{options.Id}' does not match index id '{index.Course.Id}'.");
            }

            var sourceRoot = string.IsNullOrWhiteSpace(options.SourceRoot)
                ? null
                : Path.IsPathRooted(options.SourceRoot)
                    ? options.SourceRoot
                    : Path.Combine(environment.ContentRootPath, options.SourceRoot);
            return FromIndex(index, sourceRoot);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return new CourseCorpus(
                options.Id,
                null,
                null,
                $"The course index could not be loaded: {exception.Message}");
        }
    }

    public bool TryResolveDocument(string documentId, out string documentPath)
    {
        documentPath = string.Empty;
        if (Index is null || !SourceAvailable)
        {
            return false;
        }

        var document = Index.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(
            SourceRoot!,
            document.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var allowedPrefix = string.Concat(SourceRoot, Path.DirectorySeparatorChar);
        if (!candidate.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !SupportedDocumentExtensions.Contains(Path.GetExtension(candidate)) ||
            !File.Exists(candidate))
        {
            return false;
        }

        documentPath = candidate;
        return true;
    }

    private static string? NormalizeSourceRoot(string? sourceRoot) =>
        string.IsNullOrWhiteSpace(sourceRoot) ? null : Path.GetFullPath(sourceRoot);
}
