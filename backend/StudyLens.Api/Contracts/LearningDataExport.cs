namespace StudyLens.Api.Contracts;

public sealed record LearningDataExport(
    int SchemaVersion,
    DateTime ExportedAtUtc,
    string ParticipantId,
    IReadOnlyList<LearningEventResponse> Events);
