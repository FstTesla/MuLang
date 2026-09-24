namespace MuLang.Core.Evaluation;

internal sealed class PrimitiveOperationException : Exception
{
    public PrimitiveOperationException(
        PrimitiveOperationError error,
        string message,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        Error = error;
    }

    public PrimitiveOperationError Error { get; }
}
