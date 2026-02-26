namespace Api.Models;

public class Job
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = "BlazorServer";
    public string TargetMode { get; set; } = "BlazorServer";
    public string Status { get; set; } = "created";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Error { get; set; }
    public string? Analysis { get; set; }
    public string? PlaybookMd { get; set; }
    public string? TrainingMd { get; set; }
}
