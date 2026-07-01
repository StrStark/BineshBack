namespace Binesh.Application.Exceptions;

/// <summary>
/// Base for every exception the application throws on purpose.
/// The global exception handler maps subclasses to HTTP status codes.
/// Anything that is NOT a subclass is a bug and becomes 500.
///
/// Named AppException (not ApplicationException) to avoid collision with
/// System.ApplicationException, which is a different, deprecated type.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }

    protected AppException(string message, Exception inner) : base(message, inner) { }

    /// <summary>The HTTP status this exception maps to.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Stable machine-readable error code for clients (e.g. "user.not_found").</summary>
    public abstract string ErrorCode { get; }
}
