namespace KnowledgeHub.Application.DTOs;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public string? TraceId { get; init; }

    public T? Data { get; init; }

    public IReadOnlyCollection<string> Errors { get; init; }
        = [];
}