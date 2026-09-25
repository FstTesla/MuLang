namespace MuLang.IR.Serialization;

/// <summary>Represents a failure to encode an IR program as MuIR.</summary>
public sealed class MuIrSerializationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MuIrSerializationException" /> class.</summary>
    /// <param name="message">The error message.</param>
    public MuIrSerializationException(string message)
        : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="MuIrSerializationException" /> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public MuIrSerializationException(string message, Exception innerException)
        : base(message, innerException) { }
}
