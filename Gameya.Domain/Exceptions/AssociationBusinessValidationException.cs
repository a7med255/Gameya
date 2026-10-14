namespace Gameya.Domain.Exceptions;

public sealed record AssociationValidationError(
    string Code,
    string Field,
    int? PeriodNumber,
    string Message);

public sealed class AssociationBusinessValidationException(
    IReadOnlyCollection<AssociationValidationError> errors)
    : Exception("The association configuration contains business validation errors.")
{
    public IReadOnlyCollection<AssociationValidationError> Errors { get; } = errors;
}
