using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using StudyLens.Api.Data;
using StudyLens.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();
if (args.Length > 0)
{
    builder.Configuration.AddCommandLine(args);
}

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });

builder.Services.Configure<CourseCatalogOptions>(
    builder.Configuration.GetSection(CourseCatalogOptions.SectionName));
builder.Services.Configure<TutorAiOptions>(
    builder.Configuration.GetSection(TutorAiOptions.SectionName));
builder.Services.Configure<CourseImportOptions>(
    builder.Configuration.GetSection(CourseImportOptions.SectionName));
builder.Services.Configure<StudyDataOptions>(
    builder.Configuration.GetSection(StudyDataOptions.SectionName));
builder.Services.Configure<MongoDbOptions>(
    builder.Configuration.GetSection(MongoDbOptions.SectionName));

builder.Services.AddSingleton<CourseCatalog>();
builder.Services.AddSingleton<CourseSearchService>();
builder.Services.AddSingleton<CoursePagePreviewService>();
builder.Services.AddSingleton<ICourseIndexBuilder, PythonCourseIndexBuilder>();
builder.Services.AddSingleton<CourseImportService>();
builder.Services.AddSingleton<ITutorAiProviderRegistry, TutorAiProviderRegistry>();
builder.Services.AddScoped<TutorService>();
builder.Services.AddHttpClient();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 300L * 1024 * 1024;
});

var studyDataProvider = builder.Configuration[$"{StudyDataOptions.SectionName}:Provider"] ?? "MongoDb";
if (studyDataProvider.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IStudyAttemptRepository, MongoStudyAttemptRepository>();
}
else if (studyDataProvider.Equals("LocalJson", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IStudyAttemptRepository, LocalJsonStudyAttemptRepository>();
}
else
{
    throw new InvalidOperationException(
        $"Unsupported StudyData:Provider '{studyDataProvider}'. Use 'MongoDb' or 'LocalJson'.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalClients", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("LocalClients");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/health", async (
    CourseSearchService courseSearch,
    IStudyAttemptRepository studyAttempts,
    CancellationToken cancellationToken) =>
{
    var storage = await studyAttempts.GetStatusAsync(cancellationToken);
    var response = new
    {
        status = storage.Available ? "healthy" : "degraded",
        readyCourses = courseSearch.GetStatuses().Count(course => course.Ready),
        configuredCourses = courseSearch.GetStatuses().Count,
        storage,
    };

    return Results.Json(response, statusCode: storage.Available ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
