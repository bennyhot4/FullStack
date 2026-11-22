namespace HourOffice.Api.Services;

/// <summary>A business-rule failure: the HTTP status and the error code the REST API returns as
/// <c>{ code, message }</c>. The MCP tools return the same code.</summary>
public sealed record ServiceError(int Status, string Code, string? Message = null)
{
    public static ServiceError BadRequest(string code, string? message = null) => new(400, code, message);
    public static ServiceError NotFound(string code = "not_found") => new(404, code);
    public static ServiceError Conflict(string code, string? message = null) => new(409, code, message);
    public static ServiceError Forbidden(string code = "forbidden") => new(403, code);
}

/// <summary>Either a value or a <see cref="ServiceError"/>. Shared services return it so the REST endpoints
/// and the MCP tools apply exactly the same rules (plan 011, S2).</summary>
public readonly struct Outcome<T>
{
    private Outcome(T? value, ServiceError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ServiceError? Error { get; }

    public static implicit operator Outcome<T>(T value) => new(value, null);
    public static implicit operator Outcome<T>(ServiceError error) => new(default, error);
}
