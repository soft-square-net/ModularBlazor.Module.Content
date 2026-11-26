namespace soft.square.net.WebApi.Modules.Content.UseCases;

public static class ResultExtensions
{
    /// <summary>A failed <see cref="Result"/> as a <see cref="Result{T}"/>: same status, same messages.</summary>
    public static Result<T> As<T>(this Ardalis.Result.IResult failure) => failure.Status switch
    {
        ResultStatus.Forbidden => Result<T>.Forbidden([.. failure.Errors]),
        ResultStatus.NotFound => Result<T>.NotFound([.. failure.Errors]),
        ResultStatus.Conflict => Result<T>.Conflict([.. failure.Errors]),
        ResultStatus.Unauthorized => Result<T>.Unauthorized([.. failure.Errors]),
        ResultStatus.Invalid => Result<T>.Invalid([.. failure.ValidationErrors]),
        ResultStatus.Unavailable => Result<T>.Unavailable([.. failure.Errors]),
        ResultStatus.Ok => throw new InvalidOperationException("Only a failed result converts."),
        _ => Result<T>.Error(new ErrorList([.. failure.Errors]))
    };

    /// <summary>A failed result of any type as a plain <see cref="Result"/>: same status, same messages.</summary>
    public static Result AsResult(this Ardalis.Result.IResult failure) => failure.Status switch
    {
        ResultStatus.Forbidden => Result.Forbidden([.. failure.Errors]),
        ResultStatus.NotFound => Result.NotFound([.. failure.Errors]),
        ResultStatus.Conflict => Result.Conflict([.. failure.Errors]),
        ResultStatus.Unauthorized => Result.Unauthorized([.. failure.Errors]),
        ResultStatus.Invalid => Result.Invalid([.. failure.ValidationErrors]),
        ResultStatus.Unavailable => Result.Unavailable([.. failure.Errors]),
        ResultStatus.Ok => throw new InvalidOperationException("Only a failed result converts."),
        _ => Result.Error(new ErrorList([.. failure.Errors]))
    };
}
