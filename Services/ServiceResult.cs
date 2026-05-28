namespace MyProjectBase.Services;

public enum ServiceResultKind
{
    // Permet aux ViewModels de distinguer une erreur reseau, une absence de fichier ou une erreur de validation.
    Ok,
    NotFound,
    Validation,
    Network,
    Unavailable,
    Error
}

public sealed record ServiceResult(bool Success, string Message, ServiceResultKind Kind = ServiceResultKind.Ok)
{
    // Resultat standard pour les operations sans valeur de retour.
    public static ServiceResult Ok(string message = "") => new(true, message);
    public static ServiceResult Fail(string message, ServiceResultKind kind = ServiceResultKind.Error) => new(false, message, kind);
    public static ServiceResult NotFound(string message) => new(false, message, ServiceResultKind.NotFound);
    public static ServiceResult Network(string message) => new(false, message, ServiceResultKind.Network);
    public static ServiceResult Unavailable(string message) => new(false, message, ServiceResultKind.Unavailable);
}

public sealed record ServiceResult<T>(bool Success, T? Value, string Message, ServiceResultKind Kind = ServiceResultKind.Ok)
{
    // Resultat generique pour les operations qui retournent une donnee, par exemple un utilisateur ou une liste.
    public static ServiceResult<T> Ok(T value, string message = "") => new(true, value, message);
    public static ServiceResult<T> Fail(string message, ServiceResultKind kind = ServiceResultKind.Error) => new(false, default, message, kind);
    public static ServiceResult<T> NotFound(string message) => new(false, default, message, ServiceResultKind.NotFound);
    public static ServiceResult<T> Network(string message) => new(false, default, message, ServiceResultKind.Network);
    public static ServiceResult<T> Unavailable(string message) => new(false, default, message, ServiceResultKind.Unavailable);
}
