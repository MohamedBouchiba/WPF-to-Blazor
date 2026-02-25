using Api.Models;

namespace Api.Data;

public interface IJobRepository
{
    Task<Job> CreateJobAsync(Job job);
    Task<Job?> GetJobAsync(Guid jobId);
    Task<List<Job>> GetJobsByUserAsync(Guid userId);
    Task UpdateJobAsync(Job job);
    Task<bool> TryLockJobForProcessingAsync(Guid jobId, string fromStatus, string toStatus);

    Task CreateJobFileAsync(JobFile file);
    Task<List<JobFile>> GetJobFilesByKindAsync(Guid jobId, string kind);
    Task<List<JobFile>> GetAllJobFilesAsync(Guid jobId);

    Task AddLogAsync(Guid jobId, string level, string message);
    Task<List<JobLog>> GetLogsAsync(Guid jobId);
}
