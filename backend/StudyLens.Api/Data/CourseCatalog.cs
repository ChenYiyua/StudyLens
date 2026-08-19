using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public sealed class CourseCatalog
{
    private readonly ConcurrentDictionary<string, CourseCorpus> courses;

    public CourseCatalog(IWebHostEnvironment environment, IOptions<CourseCatalogOptions> options)
        : this(
            options.Value.DefaultCourseId,
            LoadConfiguredAndImported(environment, options.Value))
    {
    }

    private CourseCatalog(string defaultCourseId, IEnumerable<CourseCorpus> courses)
    {
        this.courses = new ConcurrentDictionary<string, CourseCorpus>(
            courses.ToDictionary(course => course.CourseId, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        DefaultCourseId = this.courses.ContainsKey(defaultCourseId)
            ? defaultCourseId
            : this.courses.Keys.FirstOrDefault() ?? defaultCourseId;
    }

    public string DefaultCourseId { get; }

    public IReadOnlyCollection<CourseCorpus> Courses => courses.Values.ToArray();

    public static CourseCatalog FromCorpora(string defaultCourseId, params CourseCorpus[] courses) =>
        new(defaultCourseId, courses);

    public void AddOrReplace(CourseCorpus corpus) => courses[corpus.CourseId] = corpus;

    public bool TryGetCourse(string courseId, out CourseCorpus corpus) =>
        courses.TryGetValue(courseId, out corpus!);

    private static IEnumerable<CourseCorpus> LoadConfiguredAndImported(
        IWebHostEnvironment environment,
        CourseCatalogOptions options)
    {
        var configured = options.Courses
            .Select(course => CourseCorpus.Load(environment, course))
            .ToDictionary(course => course.CourseId, StringComparer.OrdinalIgnoreCase);
        var importedRoot = Path.IsPathRooted(options.ImportedCoursesRoot)
            ? options.ImportedCoursesRoot
            : Path.Combine(environment.ContentRootPath, options.ImportedCoursesRoot);

        if (Directory.Exists(importedRoot))
        {
            foreach (var indexPath in Directory.EnumerateFiles(
                importedRoot,
                "index.json",
                SearchOption.AllDirectories))
            {
                try
                {
                    var index = JsonSerializer.Deserialize<CourseIndex>(
                        File.ReadAllText(indexPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (index is null || configured.ContainsKey(index.Course.Id))
                    {
                        continue;
                    }

                    configured[index.Course.Id] = CourseCorpus.Load(
                        environment,
                        new CourseSourceOptions
                        {
                            Id = index.Course.Id,
                            IndexPath = indexPath,
                            SourceRoot = Path.Combine(Path.GetDirectoryName(indexPath)!, "source"),
                        });
                }
                catch (Exception exception) when (exception is IOException or JsonException)
                {
                    // Ignore incomplete imports. They remain local for inspection and can be retried.
                }
            }
        }

        return configured.Values;
    }
}
