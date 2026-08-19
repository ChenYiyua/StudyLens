using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;

namespace StudyLens.Api.Services;

public sealed partial class CourseImportService(
    IWebHostEnvironment environment,
    IOptions<CourseImportOptions> options,
    ICourseIndexBuilder indexBuilder,
    CourseSearchService searchService)
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".pdf", ".md", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    private readonly CourseImportOptions settings = options.Value;

    public async Task<CourseStatusResponse> ImportAsync(
        string courseName,
        IReadOnlyList<IFormFile> files,
        IReadOnlyList<string>? relativePaths,
        CancellationToken cancellationToken)
    {
        var normalizedName = courseName.Trim();
        if (normalizedName.Length is < 2 or > 100)
        {
            throw new InvalidDataException("Course name must contain between 2 and 100 characters.");
        }

        if (files.Count is < 1 || files.Count > 100 || files.Count > settings.MaximumFileCount)
        {
            throw new InvalidDataException(
                $"Choose between 1 and {Math.Min(100, settings.MaximumFileCount)} course files.");
        }

        if (relativePaths is { Count: > 0 } && relativePaths.Count != files.Count)
        {
            throw new InvalidDataException("Every uploaded file must have one matching relative path.");
        }

        var supportedFiles = files
            .Select((file, index) => new UploadCandidate(
                file,
                relativePaths is { Count: > 0 } ? relativePaths[index] : file.FileName))
            .Where(candidate => SupportedExtensions.Contains(
                Path.GetExtension(candidate.OriginalPath.Replace('\\', '/'))))
            .ToArray();
        if (supportedFiles.Length == 0)
        {
            throw new InvalidDataException(
                "Unsupported files were skipped, but no PDF, Markdown, or text files remain.");
        }

        var safeRelativePaths = supportedFiles
            .Select(candidate => CreateSafeRelativePath(candidate.OriginalPath))
            .ToArray();

        var totalBytes = supportedFiles.Sum(candidate => candidate.File.Length);
        if (totalBytes <= 0 || totalBytes > settings.MaximumCourseBytes)
        {
            throw new InvalidDataException(
                $"The course upload must be smaller than {settings.MaximumCourseBytes / 1024 / 1024} MB.");
        }

        for (var index = 0; index < supportedFiles.Length; index++)
        {
            var file = supportedFiles[index].File;

            if (file.Length <= 0 || file.Length > settings.MaximumFileBytes)
            {
                throw new InvalidDataException(
                    $"File '{Path.GetFileName(file.FileName)}' is empty or exceeds the per-file size limit.");
            }
        }

        var courseId = CreateCourseId(normalizedName);
        var importedRoot = ResolveImportedRoot();
        var courseRoot = Path.Combine(importedRoot, courseId);
        var sourceRoot = Path.Combine(courseRoot, "source");
        var indexPath = Path.Combine(courseRoot, "index.json");
        Directory.CreateDirectory(sourceRoot);

        try
        {
            for (var index = 0; index < supportedFiles.Length; index++)
            {
                var file = supportedFiles[index].File;
                var destination = CreateUniqueDestination(sourceRoot, safeRelativePaths[index]);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);
                await file.CopyToAsync(output, cancellationToken);
            }

            await indexBuilder.BuildAsync(
                sourceRoot,
                indexPath,
                courseId,
                normalizedName,
                cancellationToken);
            var corpus = CourseCorpus.Load(
                environment,
                new CourseSourceOptions
                {
                    Id = courseId,
                    IndexPath = indexPath,
                    SourceRoot = sourceRoot,
                });
            if (!corpus.Ready || corpus.Index?.Statistics.ChunkCount == 0)
            {
                throw new InvalidOperationException(
                    corpus.Error ?? "No searchable text could be extracted from the uploaded course.");
            }

            searchService.AddOrReplace(corpus);
            return searchService.TryGetStatus(courseId, out var status)
                ? status
                : throw new InvalidOperationException("Imported course was indexed but not registered.");
        }
        catch
        {
            DeleteIncompleteImport(importedRoot, courseRoot);
            throw;
        }
    }

    private string ResolveImportedRoot()
    {
        var configured = settings.ImportedCoursesRoot;
        var root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
        return Path.GetFullPath(root);
    }

    private static string CreateCourseId(string courseName)
    {
        var slug = NonSlugCharacter().Replace(courseName.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > 40)
        {
            slug = slug[..40].TrimEnd('-');
        }

        if (slug.Length < 2)
        {
            slug = "course";
        }

        return $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 49)];
    }

    private static string CreateSafeRelativePath(string originalPath)
    {
        var portablePath = originalPath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(portablePath) || Path.IsPathRooted(portablePath))
        {
            throw new InvalidDataException("An uploaded file has an invalid relative path.");
        }

        var segments = portablePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
        {
            throw new InvalidDataException("Folder uploads may not contain absolute or parent paths.");
        }

        var cleanedSegments = segments
            .Select(segment => InvalidFileNameCharacter().Replace(segment, "_").Trim().Trim('.'))
            .ToArray();
        if (cleanedSegments.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("An uploaded folder contains an invalid path segment.");
        }

        return Path.Combine(cleanedSegments);
    }

    private static string CreateUniqueDestination(string sourceRoot, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(sourceRoot, relativePath));
        if (!destination.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("An uploaded file resolved outside the course folder.");
        }

        var directory = Path.GetDirectoryName(destination)!;
        var extension = Path.GetExtension(destination);
        var stem = Path.GetFileNameWithoutExtension(destination);
        var candidate = destination;
        for (var suffix = 2; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(directory, $"{stem}-{suffix}{extension}");
        }

        return candidate;
    }

    private static void DeleteIncompleteImport(string importedRoot, string courseRoot)
    {
        var normalizedRoot = Path.GetFullPath(importedRoot).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var normalizedCourse = Path.GetFullPath(courseRoot);
        if (normalizedCourse.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(normalizedCourse))
        {
            Directory.Delete(normalizedCourse, recursive: true);
        }
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacter();

    [GeneratedRegex("[<>:\"/\\|?*\\x00-\\x1F]")]
    private static partial Regex InvalidFileNameCharacter();

    private sealed record UploadCandidate(IFormFile File, string OriginalPath);
}
