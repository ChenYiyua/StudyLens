namespace StudyLens.Api.Data;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = "studylens";
    public string CollectionName { get; init; } = "learning_events";
}
