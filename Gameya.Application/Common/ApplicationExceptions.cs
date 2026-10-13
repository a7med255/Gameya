namespace Gameya.Application.Common;

public abstract class GameyaApplicationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : GameyaApplicationException(code, message);
public sealed class ForbiddenException(string code = "authorization.forbidden", string message = "Access is denied.") : GameyaApplicationException(code, message);
public sealed class ConflictException(string code, string message) : GameyaApplicationException(code, message);
public sealed class AuthenticationException(string code, string message) : GameyaApplicationException(code, message);

public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : GameyaApplicationException("validation.failed", "One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
