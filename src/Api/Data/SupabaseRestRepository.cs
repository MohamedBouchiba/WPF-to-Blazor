using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models;

namespace Api.Data;

public class SupabaseRestRepository : IJobRepository
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SupabaseRestRepository(string supabaseUrl, string serviceRoleKey)
    {
        _http = new HttpClient { BaseAddress = new Uri(supabaseUrl) };
        _http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        _http.DefaultRequestHeaders.Add("Prefer", "return=representation");
    }

    public async Task<Job> CreateJobAsync(Job job)
    {
        job.Id = Guid.NewGuid();
        job.CreatedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        var body = JsonSerializer.Serialize(job, _jsonOpts);
        var resp = await _http.PostAsync("/rest/v1/jobs", new StringContent(body, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Job>>(json, _jsonOpts);
        return items?.FirstOrDefault() ?? job;
    }

    public async Task<Job?> GetJobAsync(Guid jobId)
    {
        var resp = await _http.GetAsync($"/rest/v1/jobs?id=eq.{jobId}&select=*");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Job>>(json, _jsonOpts);
        return items?.FirstOrDefault();
    }

    public async Task<List<Job>> GetJobsByUserAsync(Guid userId)
    {
        var resp = await _http.GetAsync($"/rest/v1/jobs?user_id=eq.{userId}&select=id,user_id,name,target,status,created_at,updated_at,error&order=created_at.desc");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Job>>(json, _jsonOpts) ?? [];
    }

    public async Task UpdateJobAsync(Job job)
    {
        job.UpdatedAt = DateTime.UtcNow;
        var payload = new
        {
            status = job.Status,
            error = job.Error,
            analysis = job.Analysis,
            playbook_md = job.PlaybookMd,
            training_md = job.TrainingMd,
            updated_at = job.UpdatedAt
        };
        var body = JsonSerializer.Serialize(payload, _jsonOpts);

        using var req = new HttpRequestMessage(HttpMethod.Patch, $"/rest/v1/jobs?id=eq.{job.Id}");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<bool> TryLockJobForProcessingAsync(Guid jobId, string fromStatus, string toStatus)
    {
        var payload = JsonSerializer.Serialize(new { status = toStatus, updated_at = DateTime.UtcNow }, _jsonOpts);

        using var req = new HttpRequestMessage(HttpMethod.Patch, $"/rest/v1/jobs?id=eq.{jobId}&status=eq.{fromStatus}");
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        req.Headers.Add("Prefer", "return=representation,count=exact");
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Job>>(json, _jsonOpts);
        return items?.Count > 0;
    }

    public async Task CreateJobFileAsync(JobFile file)
    {
        file.Id = Guid.NewGuid();
        file.CreatedAt = DateTime.UtcNow;

        var body = JsonSerializer.Serialize(file, _jsonOpts);
        var resp = await _http.PostAsync("/rest/v1/job_files", new StringContent(body, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
    }

    public async Task<List<JobFile>> GetJobFilesByKindAsync(Guid jobId, string kind)
    {
        var resp = await _http.GetAsync($"/rest/v1/job_files?job_id=eq.{jobId}&kind=eq.{kind}&select=*&order=path");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<JobFile>>(json, _jsonOpts) ?? [];
    }

    public async Task<List<JobFile>> GetAllJobFilesAsync(Guid jobId)
    {
        var resp = await _http.GetAsync($"/rest/v1/job_files?job_id=eq.{jobId}&select=*&order=kind,path");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<JobFile>>(json, _jsonOpts) ?? [];
    }

    public async Task AddLogAsync(Guid jobId, string level, string message)
    {
        var log = new { id = Guid.NewGuid(), job_id = jobId, ts = DateTime.UtcNow, level, message };
        var body = JsonSerializer.Serialize(log, _jsonOpts);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/job_logs");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        req.Headers.Remove("Prefer");
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<List<JobLog>> GetLogsAsync(Guid jobId)
    {
        var resp = await _http.GetAsync($"/rest/v1/job_logs?job_id=eq.{jobId}&select=*&order=ts");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<JobLog>>(json, _jsonOpts) ?? [];
    }
}
