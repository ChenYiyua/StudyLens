using System.Text.Json.Serialization;
using StudyLens.Api.Data;
using StudyLens.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });

builder.Services.Configure<MongoDbOptions>(builder.Configuration.GetSection(MongoDbOptions.SectionName));

var storageProvider = builder.Configuration["Storage:Provider"] ?? "InMemory";
if (storageProvider.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ILearningEventRepository, MongoLearningEventRepository>();
}
else
{
    builder.Services.AddSingleton<ILearningEventRepository, InMemoryLearningEventRepository>();
}

builder.Services.AddSingleton<LearningInsightsService>();

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
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", storage = storageProvider }));

app.Run();

public partial class Program;
