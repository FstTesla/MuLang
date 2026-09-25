namespace MuLang.IR.Serialization;

/// <summary>Provides stable diagnostic codes produced while reading MuIR.</summary>
public static class MuIrDiagnosticCodes
{
    /// <summary>Gets the code for an invalid MuIR magic header.</summary>
    public const string InvalidMagic = "MUIR0001";

    /// <summary>Gets the code for an unsupported MuIR format version.</summary>
    public const string UnsupportedVersion = "MUIR0002";

    /// <summary>Gets the code for an invalid lexical token.</summary>
    public const string InvalidToken = "MUIR0003";

    /// <summary>Gets the code for an unterminated string literal.</summary>
    public const string UnterminatedString = "MUIR0004";

    /// <summary>Gets the code for an invalid string escape.</summary>
    public const string InvalidEscape = "MUIR0005";

    /// <summary>Gets the code for invalid UTF-8 input.</summary>
    public const string InvalidUtf8 = "MUIR0006";

    /// <summary>Gets the code for a missing required section.</summary>
    public const string MissingSection = "MUIR0007";

    /// <summary>Gets the code for a duplicate declaration.</summary>
    public const string DuplicateDeclaration = "MUIR0008";

    /// <summary>Gets the code for an undefined reference.</summary>
    public const string UndefinedReference = "MUIR0009";

    /// <summary>Gets the code for an invalid numeric value.</summary>
    public const string InvalidNumber = "MUIR0010";

    /// <summary>Gets the code for numeric overflow.</summary>
    public const string NumericOverflow = "MUIR0011";

    /// <summary>Gets the code for an invalid type definition.</summary>
    public const string InvalidType = "MUIR0012";

    /// <summary>Gets the code for an invalid instruction.</summary>
    public const string InvalidInstruction = "MUIR0013";

    /// <summary>Gets the code for an invalid terminator.</summary>
    public const string InvalidTerminator = "MUIR0014";

    /// <summary>Gets the code for an invalid source span.</summary>
    public const string InvalidSpan = "MUIR0015";

    /// <summary>Gets the code for an unexpected token.</summary>
    public const string UnexpectedToken = "MUIR0016";

    /// <summary>Gets the code for trailing content.</summary>
    public const string TrailingContent = "MUIR0017";

    /// <summary>Gets the code for a configured reader limit violation.</summary>
    public const string LimitExceeded = "MUIR0018";

    /// <summary>Gets the code for a failure while constructing the IR model.</summary>
    public const string MaterializationFailed = "MUIR0019";
}
