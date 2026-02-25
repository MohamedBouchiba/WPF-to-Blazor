namespace Api.Dtos;

public record CreateJobRequest(string Name, string Target, List<FileInput> Files);

public record FileInput(string Path, string Content);
