namespace KnowledgeHub.Application.DTOs;

public static class ApiResponses
{
    public static ApiResponse<T> Success<T>(
        T data,
        int statusCode = 200,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            StatusCode = statusCode,
            TraceId = traceId,
            Data = data
        };
    }

    public static ApiResponse<T> Failure<T>(
        int statusCode,
        string? traceId = null,
        params string[] errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            StatusCode = statusCode,
            TraceId = traceId,
            Errors = errors
        };
    }
}