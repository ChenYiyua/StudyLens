using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace StudyLens.Api.Services;

public sealed class PythonCourseIndexBuilder(
    IWebHostEnvironment environment,
    IOptions<CourseImportOptions> options) : ICourseIndexBuilder
{
    private readonly CourseImportOptions settings = options.Value;

    public async Task BuildAsync(
        string sourcePath,
        string outputPath,
        string courseId,
        string courseName,
        CancellationToken cancellationToken)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
        var scriptPath = Path.Combine(repositoryRoot, "tools", "build_course_index.py");
        if (!File.Exists(scriptPath))
        {
            throw new InvalidOperationException($"Course indexer was not found: {scriptPath}");
        }

        var executable = settings.PythonExecutable;
        if (string.IsNullOrWhiteSpace(executable))
        {
            executable = OperatingSystem.IsWindows() ? "py" : "python3";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot,
        };
        if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(settings.PythonExecutable))
        {
            startInfo.ArgumentList.Add("-3.12");
        }

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("--course-id");
        startInfo.ArgumentList.Add(courseId);
        startInfo.ArgumentList.Add("--course-name");
        startInfo.ArgumentList.Add(courseName);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Python course indexer could not be started.");
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 30, 900)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(linked.Token);
            var standardError = process.StandardError.ReadToEndAsync(linked.Token);
            await process.WaitForExitAsync(linked.Token);
            var output = await standardOutput;
            var error = await standardError;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "Course indexing failed. Check that Python 3.12 and tools/requirements.txt are installed."
                        : $"Course indexing failed: {error.Trim()}");
            }

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException(
                    $"Course indexer completed without creating an index. Output: {output.Trim()}");
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new InvalidOperationException("Course indexing was cancelled or timed out.");
        }
    }
}
