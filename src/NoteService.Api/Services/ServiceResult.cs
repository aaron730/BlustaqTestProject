namespace NoteService.Api.Services;

public enum ServiceError
{
    None = 0,
    NotFound,
    Forbidden,
    Conflict,
    PreconditionFailed,
}

public readonly record struct ServiceResult(ServiceError Error)
{
    public bool IsSuccess => Error == ServiceError.None;

    public static ServiceResult Success() => new(ServiceError.None);

    public static implicit operator ServiceResult(ServiceError error) => new(error);
}

public readonly record struct ServiceResult<T>(T? Value, ServiceError Error)
{
    public bool IsSuccess => Error == ServiceError.None;

    public static implicit operator ServiceResult<T>(T value) => new(value, ServiceError.None);

    public static implicit operator ServiceResult<T>(ServiceError error) => new(default, error);
}
