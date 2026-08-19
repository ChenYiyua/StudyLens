using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StudyLens.Api.Data;

namespace StudyLens.Api.Services;

public sealed class CoursePagePreviewService(
    IWebHostEnvironment environment,
    IOptions<CourseImportOptions> options,
    CourseCatalog catalog)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RenderLocks = new(
        StringComparer.OrdinalIgnoreCase);

    private readonly CourseImportOptions settings = options.Value;

    public async Task<string?> GetOrCreateAsync(
        string courseId,
        string documentId,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        if (!catalog.TryGetCourse(courseId, out var corpus) ||
            corpus.Index is null ||
            !corpus.TryResolveDocument(documentId, out var documentPath) ||
            !Path.GetExtension(documentPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var document = corpus.Index.Documents.FirstOrDefault(item =>
            item.Id.Equals(documentId, StringComparison.OrdinalIgnoreCase));
        if (document is null || pageNumber < 1 || pageNumber > document.PageCount)
        {
            return null;
        }

        var cacheRoot = Path.Combine(environment.ContentRootPath, "App_Data", "page-previews");
        Directory.CreateDirectory(cacheRoot);
        var cacheKey = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{courseId}:{documentId}"))).ToLowerInvariant()[..24];
        var outputPath = Path.Combine(cacheRoot, $"{cacheKey}-p{pageNumber}.png");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        var renderLock = RenderLocks.GetOrAdd(outputPath, _ => new SemaphoreSlim(1, 1));
        await renderLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            var temporaryOutput = $"{outputPath}.{Guid.NewGuid():N}.tmp.png";
            try
            {
                await RenderAsync(documentPath, temporaryOutput, pageNumber, cancellationToken);
                File.Move(temporaryOutput, outputPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryOutput))
                {
                    File.Delete(temporaryOutput);
                }
            }

            return outputPath;
        }
        finally
        {
            renderLock.Release();
        }
    }

    private async Task RenderAsync(
        string documentPath,
        string outputPath,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
        var scriptPath = Path.Combine(repositoryRoot, "tools", "render_pdf_page.py");
        if (!File.Exists(scriptPath))
        {
            throw new InvalidOperationException("The PDF page renderer is missing.");
        }

        var python = string.IsNullOrWhiteSpace(settings.PythonExecutable)
            ? OperatingSystem.IsWindows() ? "py" : "python3"
            : settings.PythonExecutable;
        var startInfo = new ProcessStartInfo
        {
            FileName = python,
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows() &&
            Path.GetFileNameWithoutExtension(python).Equals("py", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("-3.12");
        }

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(documentPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("--page-number");
        startInfo.ArgumentList.Add(pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The PDF page renderer could not be started.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Rendering this course page took longer than 60 seconds.");
        }

        var error = await standardError;
        _ = await standardOutput;
        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"The PDF page could not be rendered: {error.Trim()}".Trim());
        }
    }
}
