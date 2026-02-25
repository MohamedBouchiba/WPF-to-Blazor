namespace Api.Models;

public class JobLog
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public DateTime Ts { get; set; }
    public string Level { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
}
