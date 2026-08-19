namespace StudyLens.Api.Data;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = "mongodb://127.0.0.1:27017";

    public string DatabaseName { get; init; } = "studylens";

    public string CollectionName { get; init; } = "study_attempts";
}
