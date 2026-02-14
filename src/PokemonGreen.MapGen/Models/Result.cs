namespace PokemonGreen.MapGen.Models;

public readonly record struct Result<T>(bool IsSuccess, T Value, string ErrorMessage)
{
    public static Result<T> Ok(T value) => new(true, value, string.Empty);
    public static Result<T> Fail(string errorMessage) => new(false, default!, errorMessage);
}
