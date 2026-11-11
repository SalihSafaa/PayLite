namespace PayLite.WalletApi;

public enum ErrorType
{
    None,
    NotFound,
    Validation,
    Conflict,
    UnAuthorized,
    Forbidden,
    UnprocessableEntity
}
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ErrorType ErrorType { get; }
    public string? ErrorMessage { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        ErrorType = ErrorType.None;
        ErrorMessage = null;
    }
    private Result(ErrorType errorType, string errorMessage)
    {
        IsSuccess = false;
        Value = default;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }
    public static Result<T> Success(T value) => new Result<T>(value);
    public static Result<T> Failure(ErrorType errorType, string errorMessage) => new Result<T>(errorType, errorMessage);
}
