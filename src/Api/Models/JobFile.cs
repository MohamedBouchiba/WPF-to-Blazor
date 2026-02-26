namespace Api.Models;

public class JobFile
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string Kind { get; set; } = "input";
    public string Path { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FileType { get; set; } = "xaml";
    public string? StorageKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
