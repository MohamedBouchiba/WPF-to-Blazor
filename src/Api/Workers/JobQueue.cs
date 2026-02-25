using System.Threading.Channels;

namespace Api.Workers;

public record JobTask(Guid JobId, string Action);

public class JobQueue
{
    private readonly Channel<JobTask> _channel = Channel.CreateUnbounded<JobTask>(
        new UnboundedChannelOptions { SingleReader = true }
    );

    public async ValueTask EnqueueAsync(JobTask task, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(task, ct);
    }

    public async ValueTask<JobTask> DequeueAsync(CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }
}
