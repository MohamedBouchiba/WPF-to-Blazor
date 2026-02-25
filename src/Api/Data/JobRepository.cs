using System.Text.Json;
using Api.Models;
using Dapper;
using Npgsql;

namespace Api.Data;

public class JobRepository : IJobRepository
{
    private readonly string _connectionString;

    public JobRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Job> CreateJobAsync(Job job)
    {
        using var conn = CreateConnection();
        job.Id = Guid.NewGuid();
        job.CreatedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        await conn.ExecuteAsync(
            @"INSERT INTO jobs (id, user_id, name, target, status, created_at, updated_at)
              VALUES (@Id, @UserId, @Name, @Target, @Status, @CreatedAt, @UpdatedAt)",
            job
        );

        return job;
    }

    public async Task<Job?> GetJobAsync(Guid jobId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Job>(
            @"SELECT id, user_id as UserId, name, target, status,
                     created_at as CreatedAt, updated_at as UpdatedAt,
                     error, analysis, playbook_md as PlaybookMd, training_md as TrainingMd
              FROM jobs WHERE id = @jobId",
            new { jobId }
        );
    }

    public async Task<List<Job>> GetJobsByUserAsync(Guid userId)
    {
        using var conn = CreateConnection();
        var results = await conn.QueryAsync<Job>(
            @"SELECT id, user_id as UserId, name, target, status,
                     created_at as CreatedAt, updated_at as UpdatedAt,
                     error
              FROM jobs WHERE user_id = @userId ORDER BY created_at DESC",
            new { userId }
        );
        return results.ToList();
    }

    public async Task UpdateJobAsync(Job job)
    {
        job.UpdatedAt = DateTime.UtcNow;
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE jobs SET status = @Status, error = @Error, analysis = @Analysis::jsonb,
                     playbook_md = @PlaybookMd, training_md = @TrainingMd,
                     updated_at = @UpdatedAt
              WHERE id = @Id",
            job
        );
    }

    public async Task<bool> TryLockJobForProcessingAsync(Guid jobId, string fromStatus, string toStatus)
    {
        using var conn = CreateConnection();
        var rows = await conn.ExecuteAsync(
            @"UPDATE jobs SET status = @toStatus, updated_at = now()
              WHERE id = @jobId AND status = @fromStatus",
            new { jobId, fromStatus, toStatus }
        );
        return rows > 0;
    }

    public async Task CreateJobFileAsync(JobFile file)
    {
        file.Id = Guid.NewGuid();
        file.CreatedAt = DateTime.UtcNow;
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"INSERT INTO job_files (id, job_id, kind, path, content, storage_key, created_at)
              VALUES (@Id, @JobId, @Kind, @Path, @Content, @StorageKey, @CreatedAt)",
            file
        );
    }

    public async Task<List<JobFile>> GetJobFilesByKindAsync(Guid jobId, string kind)
    {
        using var conn = CreateConnection();
        var results = await conn.QueryAsync<JobFile>(
            @"SELECT id, job_id as JobId, kind, path, content, storage_key as StorageKey, created_at as CreatedAt
              FROM job_files WHERE job_id = @jobId AND kind = @kind ORDER BY path",
            new { jobId, kind }
        );
        return results.ToList();
    }

    public async Task<List<JobFile>> GetAllJobFilesAsync(Guid jobId)
    {
        using var conn = CreateConnection();
        var results = await conn.QueryAsync<JobFile>(
            @"SELECT id, job_id as JobId, kind, path, content, storage_key as StorageKey, created_at as CreatedAt
              FROM job_files WHERE job_id = @jobId ORDER BY kind, path",
            new { jobId }
        );
        return results.ToList();
    }

    public async Task AddLogAsync(Guid jobId, string level, string message)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"INSERT INTO job_logs (id, job_id, ts, level, message)
              VALUES (@id, @jobId, @ts, @level, @message)",
            new { id = Guid.NewGuid(), jobId, ts = DateTime.UtcNow, level, message }
        );
    }

    public async Task<List<JobLog>> GetLogsAsync(Guid jobId)
    {
        using var conn = CreateConnection();
        var results = await conn.QueryAsync<JobLog>(
            @"SELECT id, job_id as JobId, ts, level, message
              FROM job_logs WHERE job_id = @jobId ORDER BY ts",
            new { jobId }
        );
        return results.ToList();
    }
}
