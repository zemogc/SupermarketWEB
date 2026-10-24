namespace SupermarketWEB.Services;

/// <summary>
/// Resultado de una operación de negocio: el valor producido o los errores que la impidieron.
/// </summary>
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, IReadOnlyList<string> errors)
    {
        Value = value;
        Errors = errors;
    }

    public T? Value { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool Succeeded => Errors.Count == 0;

    public static ServiceResult<T> Success(T value) => new(value, []);

    public static ServiceResult<T> Failure(params string[] errors) => new(default, errors);

    public static ServiceResult<T> Failure(IEnumerable<string> errors) => new(default, errors.ToList());
}
