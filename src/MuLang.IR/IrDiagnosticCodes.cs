namespace MuLang.IR;

/// <summary>Defines diagnostic codes produced while validating portable IR.</summary>
public static class IrDiagnosticCodes
{
    /// <summary>Gets the code for invalid IR structure.</summary>
    public const string InvalidStructure = "MUL5001";

    /// <summary>Gets the code for an invalid IR slot.</summary>
    public const string InvalidSlot = "MUL5002";

    /// <summary>Gets the code for an IR type mismatch.</summary>
    public const string TypeMismatch = "MUL5003";

    /// <summary>Gets the code for an undefined provider symbol.</summary>
    public const string UndefinedProviderSymbol = "MUL5004";

    /// <summary>Gets the code for use of a slot before definition.</summary>
    public const string UseBeforeDefinition = "MUL5005";

    /// <summary>Gets the code for an environment fingerprint mismatch.</summary>
    public const string EnvironmentMismatch = "MUL5006";

    /// <summary>Gets the code for an undefined user function.</summary>
    public const string UndefinedUserFunction = "MUL5007";

    /// <summary>Gets the code for incompatible compilation metadata.</summary>
    public const string CompilationMetadataMismatch = "MUL5008";
}
