namespace TaskManager.Tasks.Domain.Exceptions;

/// <summary>
/// Thrown by the Infrastructure unit of work when EF Core detects an optimistic-concurrency
/// conflict (xmin mismatch). Application handlers catch this and map it to
/// <c>Result.Fail("conflict: ...")</c>, which Presentation turns into HTTP 409.
/// Lives in Domain so Application never references EF Core.
/// </summary>
public class ConcurrencyConflictException(string message, Exception? inner = null)
    : Exception(message, inner);
