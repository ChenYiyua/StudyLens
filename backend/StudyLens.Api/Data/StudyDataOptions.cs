namespace StudyLens.Api.Data;

public sealed class StudyDataOptions
{
    public const string SectionName = "StudyData";

    public string Provider { get; init; } = "MongoDb";

    public string LocalFilePath { get; init; } = "App_Data/study-attempts.json";

    public int RetentionLimit { get; init; } = 5000;
}
