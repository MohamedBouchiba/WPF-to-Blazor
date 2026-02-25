using System.Text.RegularExpressions;
using Api.Ai;
using Api.Ai.Prompts;
using Api.Data;
using Api.Models;

namespace Api.Workers;

public class JobWorker : BackgroundService
{
    private readonly JobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobWorker> _logger;

    public JobWorker(JobQueue queue, IServiceScopeFactory scopeFactory, ILogger<JobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var task = await _queue.DequeueAsync(stoppingToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                var ai = scope.ServiceProvider.GetRequiredService<IAiClient>();

                switch (task.Action)
                {
                    case "analyze":
                        await RunAnalyzeAsync(task.JobId, repo, ai, stoppingToken);
                        break;
                    case "convert":
                        await RunConvertAsync(task.JobId, repo, ai, stoppingToken);
                        break;
                    case "playbook":
                        await RunPlaybookAsync(task.JobId, repo, ai, stoppingToken);
                        break;
                    case "training":
                        await RunTrainingAsync(task.JobId, repo, ai, stoppingToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobId} action {Action}", task.JobId, task.Action);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                    var job = await repo.GetJobAsync(task.JobId);
                    if (job != null)
                    {
                        job.Status = "Failed";
                        job.Error = ex.Message;
                        await repo.UpdateJobAsync(job);
                        await repo.AddLogAsync(task.JobId, "error", ex.Message);
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to update job status for {JobId}", task.JobId);
                }
            }
        }
    }

    private async Task RunAnalyzeAsync(Guid jobId, IJobRepository repo, IAiClient ai, CancellationToken ct)
    {
        if (!await repo.TryLockJobForProcessingAsync(jobId, "Created", "Analyzing"))
        {
            _logger.LogWarning("Job {JobId} is not in Created state, skipping analyze", jobId);
            return;
        }

        await repo.AddLogAsync(jobId, "info", "Starting analysis");

        var inputFiles = await repo.GetJobFilesByKindAsync(jobId, "input");
        var fileDict = inputFiles
            .Where(f => f.Content != null)
            .ToDictionary(f => f.Path, f => f.Content!);

        var userPrompt = AnalyzePrompt.BuildUserPrompt(fileDict);
        await repo.AddLogAsync(jobId, "info", $"Sending {fileDict.Count} files to AI for analysis");

        var result = await ai.CompleteAsync(AnalyzePrompt.System, userPrompt, ct);

        var job = await repo.GetJobAsync(jobId);
        if (job == null) return;

        job.Analysis = result;
        job.Status = "Created";
        await repo.UpdateJobAsync(job);
        await repo.AddLogAsync(jobId, "info", "Analysis completed");
    }

    private async Task RunConvertAsync(Guid jobId, IJobRepository repo, IAiClient ai, CancellationToken ct)
    {
        var job = await repo.GetJobAsync(jobId);
        if (job == null) return;

        if (string.IsNullOrEmpty(job.Analysis))
        {
            await repo.AddLogAsync(jobId, "info", "No analysis found, running analysis first");
            await RunAnalyzeAsync(jobId, repo, ai, ct);
            job = await repo.GetJobAsync(jobId);
            if (job == null) return;
        }

        if (!await repo.TryLockJobForProcessingAsync(jobId, "Created", "Converting"))
        {
            _logger.LogWarning("Job {JobId} is not in Created state, skipping convert", jobId);
            return;
        }

        await repo.AddLogAsync(jobId, "info", "Starting conversion");

        var inputFiles = await repo.GetJobFilesByKindAsync(jobId, "input");
        var fileDict = inputFiles
            .Where(f => f.Content != null)
            .ToDictionary(f => f.Path, f => f.Content!);

        var systemPrompt = ConvertPrompt.BuildSystemPrompt(job.Target);
        var userPrompt = ConvertPrompt.BuildUserPrompt(fileDict, job.Analysis);

        await repo.AddLogAsync(jobId, "info", $"Sending {fileDict.Count} files to AI for conversion");

        var result = await ai.CompleteAsync(systemPrompt, userPrompt, ct);

        var parsedFiles = ParseFileBlocks(result);
        await repo.AddLogAsync(jobId, "info", $"Received {parsedFiles.Count} converted files");

        foreach (var (path, content) in parsedFiles)
        {
            await repo.CreateJobFileAsync(new JobFile
            {
                JobId = jobId,
                Kind = "output",
                Path = path,
                Content = content
            });
        }

        job = await repo.GetJobAsync(jobId);
        if (job == null) return;
        job.Status = "Succeeded";
        await repo.UpdateJobAsync(job);
        await repo.AddLogAsync(jobId, "info", "Conversion completed");
    }

    private async Task RunPlaybookAsync(Guid jobId, IJobRepository repo, IAiClient ai, CancellationToken ct)
    {
        var job = await repo.GetJobAsync(jobId);
        if (job == null) return;

        var lang = job.PlaybookMd?.StartsWith("LANG:") == true
            ? job.PlaybookMd.Substring(5, 2)
            : "fr";

        job.PlaybookMd = null;
        await repo.UpdateJobAsync(job);

        await repo.AddLogAsync(jobId, "info", $"Generating playbook in {lang}");

        var inputFiles = await repo.GetJobFilesByKindAsync(jobId, "input");
        var fileDict = inputFiles
            .Where(f => f.Content != null)
            .ToDictionary(f => f.Path, f => f.Content!);

        var systemPrompt = PlaybookPrompt.BuildSystemPrompt(lang);
        var userPrompt = PlaybookPrompt.BuildUserPrompt(job.Analysis, fileDict);
        var result = await ai.CompleteAsync(systemPrompt, userPrompt, ct);

        job = await repo.GetJobAsync(jobId);
        if (job == null) return;
        job.PlaybookMd = result;
        await repo.UpdateJobAsync(job);
        await repo.AddLogAsync(jobId, "info", "Playbook generated");
    }

    private async Task RunTrainingAsync(Guid jobId, IJobRepository repo, IAiClient ai, CancellationToken ct)
    {
        var job = await repo.GetJobAsync(jobId);
        if (job == null) return;

        var lang = job.TrainingMd?.StartsWith("LANG:") == true
            ? job.TrainingMd.Substring(5, 2)
            : "fr";

        job.TrainingMd = null;
        await repo.UpdateJobAsync(job);

        await repo.AddLogAsync(jobId, "info", $"Generating training kit in {lang}");

        var outputFiles = await repo.GetJobFilesByKindAsync(jobId, "output");
        var fileDict = outputFiles
            .Where(f => f.Content != null)
            .ToDictionary(f => f.Path, f => f.Content!);

        var systemPrompt = TrainingPrompt.BuildSystemPrompt(lang);
        var userPrompt = TrainingPrompt.BuildUserPrompt(job.Analysis, fileDict);
        var result = await ai.CompleteAsync(systemPrompt, userPrompt, ct);

        job = await repo.GetJobAsync(jobId);
        if (job == null) return;
        job.TrainingMd = result;
        await repo.UpdateJobAsync(job);
        await repo.AddLogAsync(jobId, "info", "Training kit generated");
    }

    private static Dictionary<string, string> ParseFileBlocks(string aiOutput)
    {
        var files = new Dictionary<string, string>();
        var pattern = new Regex(@"FILE:\s*(.+?)\s*\n(.*?)END_FILE", RegexOptions.Singleline);

        foreach (Match match in pattern.Matches(aiOutput))
        {
            var path = match.Groups[1].Value.Trim();
            var content = match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(path))
            {
                files[path] = content;
            }
        }

        return files;
    }
}
