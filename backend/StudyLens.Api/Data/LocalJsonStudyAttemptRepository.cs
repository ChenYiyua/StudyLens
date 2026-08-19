using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public sealed class LocalJsonStudyAttemptRepository : IStudyAttemptRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath;
    private readonly int retentionLimit;

    public LocalJsonStudyAttemptRepository(
        IWebHostEnvironment environment,
        IOptions<StudyDataOptions> options)
    {
        var settings = options.Value;
        filePath = Path.IsPathRooted(settings.LocalFilePath)
            ? settings.LocalFilePath
            : Path.Combine(environment.ContentRootPath, settings.LocalFilePath);
        retentionLimit = Math.Clamp(settings.RetentionLimit, 100, 50_000);
    }

    public LocalJsonStudyAttemptRepository(string filePath, int retentionLimit = 5000)
    {
        this.filePath = Path.GetFullPath(filePath);
        this.retentionLimit = Math.Clamp(retentionLimit, 100, 50_000);
    }

    public Task<StudyDataStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new StudyDataStatus(
            "LocalJson",
            true,
            $"Fallback file store: {filePath}"));

    public async Task AddAsync(StudyAttempt attempt, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var attempts = await ReadAllAsync(cancellationToken);
            attempts.Insert(0, attempt);
            if (attempts.Count > retentionLimit)
            {
                attempts.RemoveRange(retentionLimit, attempts.Count - retentionLimit);
            }

            await WriteAllAsync(attempts, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<StudyAttempt>> GetForCourseAsync(
        string courseId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadAllAsync(cancellationToken))
                .Where(attempt => attempt.CourseId.Equals(courseId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(attempt => attempt.CreatedAtUtc)
                .Take(Math.Clamp(limit, 1, 5000))
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<long> DeleteForCourseAsync(
        string courseId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var attempts = await ReadAllAsync(cancellationToken);
            var removed = attempts.RemoveAll(
                attempt => attempt.CourseId.Equals(courseId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteAllAsync(attempts, cancellationToken);
            }

            return removed;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<StudyAttempt>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<List<StudyAttempt>>(
                stream,
                JsonOptions,
                cancellationToken) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Study history is invalid and was not overwritten: {filePath}",
                exception);
        }
    }

    private async Task WriteAllAsync(
        IReadOnlyList<StudyAttempt> attempts,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("Study history path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, attempts, JsonOptions, cancellationToken);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
