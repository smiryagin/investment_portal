namespace WiseLine.Portal.Application.Trade;

public sealed class McpTokenNameConflictException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);

public sealed class McpTokenLimitReachedException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
