using System.Diagnostics;
using System.Text.Json;
using Api.Dtos;

namespace Api.Middleware;

public class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await WriteError(context, 400, "Validation Error", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(context, 401, "Unauthorized", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteError(context, 403, "Forbidden", ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteError(context, 404, "Not Found", ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteError(context, 409, "Conflict", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteError(context, 500, "Internal Server Error", ex.Message);
        }
    }

    private static async Task WriteError(HttpContext context, int status, string title, string? detail)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var error = new ErrorResponse(
            $"https://httpstatuses.com/{status}",
            title,
            status,
            detail,
            traceId
        );

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(error);
    }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
