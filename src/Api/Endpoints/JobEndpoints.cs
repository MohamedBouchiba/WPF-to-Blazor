using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Auth;
using Api.Data;
using Api.Dtos;
using Api.Models;
using Api.Validation;
using Api.Workers;

namespace Api.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/jobs").RequireAuthorization();

        group.MapPost("/", CreateJob).WithTags("Jobs");
        group.MapGet("/", ListJobs).WithTags("Jobs");
        group.MapGet("/{jobId:guid}", GetJob).WithTags("Jobs");
        group.MapPost("/{jobId:guid}/analyze", AnalyzeJob).WithTags("Jobs");
        group.MapPost("/{jobId:guid}/convert", ConvertJob).WithTags("Jobs");
        group.MapGet("/{jobId:guid}/outputs", GetOutputs).WithTags("Jobs");
        group.MapPost("/{jobId:guid}/playbook", GeneratePlaybook).WithTags("Jobs");
        group.MapPost("/{jobId:guid}/training", GenerateTraining).WithTags("Jobs");
        group.MapGet("/{jobId:guid}/download", DownloadOutputs).WithTags("Jobs");
        group.MapGet("/{jobId:guid}/logs", GetLogs).WithTags("Jobs");
    }

    private static async Task<IResult> CreateJob(
        CreateJobRequest request,
        ClaimsPrincipal user,
        IJobRepository repo,
        FileValidator validator,
        JobQueue queue)
    {
        var userId = user.GetUserId();

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new Middleware.ValidationException("Job name is required.");

        if (request.Target != "BlazorServer" && request.Target != "BlazorWasm")
            throw new Middleware.ValidationException("Target must be BlazorServer or BlazorWasm.");

        validator.Validate(request.Files);

        var targetMode = request.Target == "BlazorWasm" ? "blazor-wasm" : "blazor-server";

        var job = await repo.CreateJobAsync(new Job
        {
            UserId = userId,
            Name = request.Name,
            Target = request.Target,
            TargetMode = targetMode
        });

        foreach (var file in request.Files)
        {
            var normalizedPath = file.Path.Replace("\\", "/");
            var filename = System.IO.Path.GetFileName(normalizedPath);
            var ext = System.IO.Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();

            await repo.CreateJobFileAsync(new JobFile
            {
                JobId = job.Id,
                Kind = "input",
                Path = normalizedPath,
                Filename = filename,
                FileType = string.IsNullOrEmpty(ext) ? "txt" : ext,
                Content = file.Content
            });
        }

        await repo.AddLogAsync(job.Id, "info", $"Job created with {request.Files.Count} input files");

        return Results.Created($"/api/jobs/{job.Id}", new JobResponse(
            job.Id, job.Name, job.Target, job.Status,
            job.CreatedAt, job.UpdatedAt, null, null, null, null
        ));
    }

    private static async Task<IResult> ListJobs(ClaimsPrincipal user, IJobRepository repo)
    {
        var userId = user.GetUserId();
        var jobs = await repo.GetJobsByUserAsync(userId);
        var response = jobs.Select(j => new JobListItem(
            j.Id, j.Name, j.Target, j.Status, j.CreatedAt, j.UpdatedAt
        ));
        return Results.Ok(response);
    }

    private static async Task<IResult> GetJob(Guid jobId, ClaimsPrincipal user, IJobRepository repo)
    {
        var job = await GetOwnedJob(jobId, user, repo);
        object? analysis = null;
        if (!string.IsNullOrEmpty(job.Analysis))
        {
            try { analysis = JsonSerializer.Deserialize<object>(job.Analysis); }
            catch { analysis = job.Analysis; }
        }

        return Results.Ok(new JobResponse(
            job.Id, job.Name, job.Target, job.Status,
            job.CreatedAt, job.UpdatedAt, job.Error,
            analysis, job.PlaybookMd, job.TrainingMd
        ));
    }

    private static async Task<IResult> AnalyzeJob(Guid jobId, ClaimsPrincipal user, IJobRepository repo, JobQueue queue)
    {
        var job = await GetOwnedJob(jobId, user, repo);

        if (!string.IsNullOrEmpty(job.Analysis))
            return Results.Ok(new { message = "Analysis already exists", jobId });

        if (job.Status != "created")
            throw new Middleware.ConflictException($"Job is in status {job.Status}, cannot analyze.");

        await queue.EnqueueAsync(new JobTask(jobId, "analyze"));
        await repo.AddLogAsync(jobId, "info", "Analysis enqueued");

        return Results.Accepted($"/api/jobs/{jobId}", new { message = "Analysis enqueued", jobId });
    }

    private static async Task<IResult> ConvertJob(Guid jobId, ClaimsPrincipal user, IJobRepository repo, JobQueue queue)
    {
        var job = await GetOwnedJob(jobId, user, repo);

        if (job.Status == "converting" || job.Status == "analyzing")
            throw new Middleware.ConflictException($"Job is already being processed (status: {job.Status}).");

        if (job.Status == "succeeded")
            throw new Middleware.ConflictException("Job has already been converted.");

        await queue.EnqueueAsync(new JobTask(jobId, "convert"));
        await repo.AddLogAsync(jobId, "info", "Conversion enqueued");

        return Results.Accepted($"/api/jobs/{jobId}", new { message = "Conversion enqueued", jobId });
    }

    private static async Task<IResult> GetOutputs(Guid jobId, ClaimsPrincipal user, IJobRepository repo)
    {
        await GetOwnedJob(jobId, user, repo);
        var files = await repo.GetJobFilesByKindAsync(jobId, "output");
        var response = files.Select(f => new JobFileResponse(f.Id, f.Kind, f.Path, f.Content, f.CreatedAt));
        return Results.Ok(response);
    }

    private static async Task<IResult> GeneratePlaybook(
        Guid jobId,
        ClaimsPrincipal user,
        IJobRepository repo,
        JobQueue queue,
        HttpContext httpContext)
    {
        var job = await GetOwnedJob(jobId, user, repo);
        var lang = httpContext.Request.Query["lang"].FirstOrDefault() ?? "fr";
        if (lang != "fr" && lang != "nl") lang = "fr";

        job.PlaybookMd = $"LANG:{lang}";
        await repo.UpdateJobAsync(job);
        await queue.EnqueueAsync(new JobTask(jobId, "playbook"));
        await repo.AddLogAsync(jobId, "info", $"Playbook generation enqueued (lang={lang})");

        return Results.Accepted($"/api/jobs/{jobId}", new { message = "Playbook generation enqueued", jobId, lang });
    }

    private static async Task<IResult> GenerateTraining(
        Guid jobId,
        ClaimsPrincipal user,
        IJobRepository repo,
        JobQueue queue,
        HttpContext httpContext)
    {
        var job = await GetOwnedJob(jobId, user, repo);
        var lang = httpContext.Request.Query["lang"].FirstOrDefault() ?? "fr";
        if (lang != "fr" && lang != "nl") lang = "fr";

        job.TrainingMd = $"LANG:{lang}";
        await repo.UpdateJobAsync(job);
        await queue.EnqueueAsync(new JobTask(jobId, "training"));
        await repo.AddLogAsync(jobId, "info", $"Training kit generation enqueued (lang={lang})");

        return Results.Accepted($"/api/jobs/{jobId}", new { message = "Training kit generation enqueued", jobId, lang });
    }

    private static async Task<IResult> DownloadOutputs(Guid jobId, ClaimsPrincipal user, IJobRepository repo)
    {
        var job = await GetOwnedJob(jobId, user, repo);
        var files = await repo.GetJobFilesByKindAsync(jobId, "output");

        if (files.Count == 0)
            throw new Middleware.ValidationException("No output files available for download.");

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file.Content)) continue;
                var entry = archive.CreateEntry(file.Path, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(file.Content);
                await entryStream.WriteAsync(bytes);
            }

            if (!string.IsNullOrEmpty(job.PlaybookMd) && !job.PlaybookMd.StartsWith("LANG:"))
            {
                var entry = archive.CreateEntry("playbook.md", CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(Encoding.UTF8.GetBytes(job.PlaybookMd));
            }

            if (!string.IsNullOrEmpty(job.TrainingMd) && !job.TrainingMd.StartsWith("LANG:"))
            {
                var entry = archive.CreateEntry("training-kit.md", CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(Encoding.UTF8.GetBytes(job.TrainingMd));
            }
        }

        memoryStream.Position = 0;
        var zipBytes = memoryStream.ToArray();
        return Results.File(zipBytes, "application/zip", $"{job.Name}-blazor-output.zip");
    }

    private static async Task<IResult> GetLogs(Guid jobId, ClaimsPrincipal user, IJobRepository repo)
    {
        await GetOwnedJob(jobId, user, repo);
        var logs = await repo.GetLogsAsync(jobId);
        var response = logs.Select(l => new LogEntry(l.Id, l.Ts, l.Level, l.Message));
        return Results.Ok(response);
    }

    private static async Task<Job> GetOwnedJob(Guid jobId, ClaimsPrincipal user, IJobRepository repo)
    {
        var userId = user.GetUserId();
        var job = await repo.GetJobAsync(jobId)
            ?? throw new Middleware.NotFoundException($"Job {jobId} not found.");

        if (job.UserId != userId)
            throw new Middleware.ForbiddenException("You do not have access to this job.");

        return job;
    }
}
