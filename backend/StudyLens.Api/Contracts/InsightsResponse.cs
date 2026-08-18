namespace StudyLens.Api.Contracts;

public sealed record DimensionCount(string Label, int Count);

public sealed record DailyUsage(DateOnly Date, int Sessions, int Minutes);

public sealed record InsightsResponse(
    int TotalSessions,
    int TotalMinutes,
    double AverageHelpfulness,
    IReadOnlyList<DimensionCount> ByProvider,
    IReadOnlyList<DimensionCount> ByActivity,
    IReadOnlyList<DailyUsage> DailyUsage);
