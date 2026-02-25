using System.Text.Json.Serialization;

namespace Api.Dtos;

public record JobResponse(
    Guid Id,
    string Name,
    string Target,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Error,
    object? Analysis,
    string? PlaybookMd,
    string? TrainingMd
);

public record JobListItem(
    Guid Id,
    string Name,
    string Target,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record JobFileResponse(
    Guid Id,
    string Kind,
    string Path,
    string? Content,
    DateTime CreatedAt
);

public record LogEntry(
    Guid Id,
    DateTime Ts,
    string Level,
    string Message
);

public record ErrorResponse(
    string Type,
    string Title,
    int Status,
    string? Detail,
    string TraceId
);
