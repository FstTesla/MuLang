namespace MuLang.Exporters.DotNet;

/// <summary>Defines error codes used by the .NET runtime exporter.</summary>
public static class DotNetRuntimeErrorCodes
{
    /// <summary>Gets the code for an unexpected null value.</summary>
    public const string NullValue = "MUL6001";

    /// <summary>Gets the code for an invalid runtime conversion.</summary>
    public const string InvalidConversion = "MUL6002";

    /// <summary>Gets the code for integer overflow.</summary>
    public const string IntegerOverflow = "MUL6003";

    /// <summary>Gets the code for division by zero.</summary>
    public const string DivisionByZero = "MUL6004";

    /// <summary>Gets the code for an invalid shift count.</summary>
    public const string InvalidShift = "MUL6005";

    /// <summary>Gets the code for an invalid array index.</summary>
    public const string InvalidIndex = "MUL6006";

    /// <summary>Gets the code for a missing object property.</summary>
    public const string MissingProperty = "MUL6007";

    /// <summary>Gets the code for rejected property mutation.</summary>
    public const string MutationRejected = "MUL6008";

    /// <summary>Gets the code for rejected property removal.</summary>
    public const string RemovalRejected = "MUL6009";

    /// <summary>Gets the code for a runtime environment mismatch.</summary>
    public const string EnvironmentMismatch = "MUL6010";

    /// <summary>Gets the code for an exhausted execution budget.</summary>
    public const string BudgetExceeded = "MUL6011";

    /// <summary>Gets the code for a missing provider global.</summary>
    public const string MissingGlobal = "MUL6012";

    /// <summary>Gets the code for a missing provider function.</summary>
    public const string MissingFunction = "MUL6013";

    /// <summary>Gets the code for a provider failure.</summary>
    public const string ProviderFailure = "MUL6014";

    /// <summary>Gets the code for an invalid runtime value.</summary>
    public const string InvalidRuntimeValue = "MUL6015";

    /// <summary>Gets the code for cancelled execution.</summary>
    public const string Cancelled = "MUL6016";

    /// <summary>Gets the code for exceeding the user-function call-depth limit.</summary>
    public const string CallDepthExceeded = "MUL6017";

    /// <summary>Gets the code for a missing user function.</summary>
    public const string MissingUserFunction = "MUL6018";
}
