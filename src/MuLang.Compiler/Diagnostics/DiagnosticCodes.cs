namespace MuLang.Compiler.Diagnostics;

/// <summary>Defines diagnostic codes produced while parsing and binding MuLang source code.</summary>
public static class DiagnosticCodes
{
    /// <summary>Gets the code for an invalid source character.</summary>
    public const string InvalidCharacter = "MUL1001";

    /// <summary>Gets the code for an unterminated string literal.</summary>
    public const string UnterminatedString = "MUL1002";

    /// <summary>Gets the code for an invalid string escape sequence.</summary>
    public const string InvalidEscapeSequence = "MUL1003";

    /// <summary>Gets the code for an invalid numeric literal.</summary>
    public const string InvalidNumber = "MUL1004";

    /// <summary>Gets the code for an unexpected token.</summary>
    public const string UnexpectedToken = "MUL2001";

    /// <summary>Gets the code for a missing expression.</summary>
    public const string ExpectedExpression = "MUL2002";

    /// <summary>Gets the code for an invalid expression statement.</summary>
    public const string InvalidExpressionStatement = "MUL2003";

    /// <summary>Gets the code for an invalid assignment target.</summary>
    public const string InvalidAssignmentTarget = "MUL2004";

    /// <summary>Gets the code for an invalid removal target.</summary>
    public const string InvalidRemovalTarget = "MUL2005";

    /// <summary>Gets the code for a chained non-associative operator.</summary>
    public const string NonAssociativeOperator = "MUL2006";

    /// <summary>Gets the code for an invalid for-loop clause.</summary>
    public const string InvalidForClause = "MUL2007";

    /// <summary>Gets the code for a disallowed trailing separator.</summary>
    public const string TrailingSeparator = "MUL2008";

    /// <summary>Gets the code for a repeated nullable annotation.</summary>
    public const string RepeatedNullableAnnotation = "MUL2009";

    /// <summary>Gets the code for a function declaration disabled by the compilation mode.</summary>
    public const string FunctionDeclarationNotAllowed = "MUL2010";

    /// <summary>Gets the code for a function declaration following a statement.</summary>
    public const string FunctionDeclarationAfterStatement = "MUL2011";

    /// <summary>Gets the code for an undefined name.</summary>
    public const string UndefinedName = "MUL3001";

    /// <summary>Gets the code for an undefined function.</summary>
    public const string UndefinedFunction = "MUL3002";

    /// <summary>Gets the code for an undefined type.</summary>
    public const string UndefinedType = "MUL3003";

    /// <summary>Gets the code for a duplicate local variable.</summary>
    public const string DuplicateLocal = "MUL3004";

    /// <summary>Gets the code for disallowed variable shadowing.</summary>
    public const string ShadowedVariable = "MUL3005";

    /// <summary>Gets the code for reading an unassigned local variable.</summary>
    public const string UnassignedLocal = "MUL3006";

    /// <summary>Gets the code for a type that cannot be inferred.</summary>
    public const string CannotInferType = "MUL3007";

    /// <summary>Gets the code for a static type mismatch.</summary>
    public const string TypeMismatch = "MUL3008";

    /// <summary>Gets the code for an operator unavailable for its operands.</summary>
    public const string OperatorNotDefined = "MUL3009";

    /// <summary>Gets the code for an invalid call target.</summary>
    public const string InvalidCallTarget = "MUL3010";

    /// <summary>Gets the code for an argument-count mismatch.</summary>
    public const string ArgumentCountMismatch = "MUL3011";

    /// <summary>Gets the code for assignment to a provider global.</summary>
    public const string CannotAssignGlobal = "MUL3012";

    /// <summary>Gets the code for a property absent from a structured type.</summary>
    public const string PropertyNotFound = "MUL3013";

    /// <summary>Gets the code for invalid indexed access.</summary>
    public const string InvalidIndex = "MUL3014";

    /// <summary>Gets the code for a property that cannot be removed.</summary>
    public const string PropertyNotRemovable = "MUL3015";

    /// <summary>Gets the code for a duplicate object property.</summary>
    public const string DuplicateObjectProperty = "MUL3016";

    /// <summary>Gets the code for an invalid explicit conversion.</summary>
    public const string InvalidConversion = "MUL3017";

    /// <summary>Gets the code for use of a void expression where a value is required.</summary>
    public const string InvalidVoidExpression = "MUL3018";

    /// <summary>Gets the code for a break statement outside a loop.</summary>
    public const string BreakOutsideLoop = "MUL4001";

    /// <summary>Gets the code for a continue statement outside a loop.</summary>
    public const string ContinueOutsideLoop = "MUL4002";

    /// <summary>Gets the code for an invalid return statement.</summary>
    public const string InvalidReturn = "MUL4003";

    /// <summary>Gets the code for a function whose paths do not all return.</summary>
    public const string NotAllPathsReturn = "MUL4004";

    /// <summary>Gets the code for an unreachable statement.</summary>
    public const string UnreachableStatement = "MUL4005";

    /// <summary>Gets the code for an integer literal outside the supported range.</summary>
    public const string InvalidIntegerLiteral = "MUL4006";

    /// <summary>Gets the code for a non-finite float literal.</summary>
    public const string InvalidFloatLiteral = "MUL4007";

    /// <summary>Gets the code for an invalid multi-level loop-control depth.</summary>
    public const string InvalidLoopLevel = "MUL4008";

    /// <summary>Gets the code for mutation of a read-only target.</summary>
    public const string ReadOnlyTarget = "MUL3019";

    /// <summary>Gets the code for a missing required object property.</summary>
    public const string MissingObjectProperty = "MUL3020";

    /// <summary>Gets the code for a declaration in an invalid embedded-statement position.</summary>
    public const string InvalidEmbeddedDeclaration = "MUL3021";

    /// <summary>Gets the code for a duplicate user-function declaration.</summary>
    public const string DuplicateFunction = "MUL3022";

    /// <summary>Gets the code for a user function conflicting with another symbol.</summary>
    public const string FunctionConflict = "MUL3023";

    /// <summary>Gets the code for a duplicate function parameter.</summary>
    public const string DuplicateParameter = "MUL3024";

    /// <summary>Gets the code for an invalid parameter type.</summary>
    public const string InvalidParameterType = "MUL3025";

    /// <summary>Gets the code for an invalid return type.</summary>
    public const string InvalidReturnType = "MUL3026";

    /// <summary>Gets the code for assignment to a function parameter.</summary>
    public const string CannotAssignParameter = "MUL3027";

    /// <summary>Gets the code for a repeated read-only type modifier.</summary>
    public const string RepeatedReadOnlyModifier = "MUL3028";

    /// <summary>Gets the code for a misplaced read-only type modifier.</summary>
    public const string InvalidReadOnlyModifierPlacement = "MUL3029";

    /// <summary>Gets the code for a type test that is statically known to be false.</summary>
    public const string ImpossibleTypeTest = "MUL3030";

    /// <summary>Gets the code for disabled user-defined functions.</summary>
    public const string DisabledUserDefinedFunctions = "MUL7001";

    /// <summary>Gets the code for disabled recursion.</summary>
    public const string DisabledRecursion = "MUL7002";

    /// <summary>Gets the code for disabled while loops.</summary>
    public const string DisabledWhileLoop = "MUL7003";

    /// <summary>Gets the code for disabled for loops.</summary>
    public const string DisabledForLoop = "MUL7004";

    /// <summary>Gets the code for disabled provider-function calls.</summary>
    public const string DisabledProviderFunctionCalls = "MUL7005";

    /// <summary>Gets the code for disabled open-object operations.</summary>
    public const string DisabledOpenObjects = "MUL7006";

    /// <summary>Gets the code for disabled object-property mutation.</summary>
    public const string DisabledObjectPropertyMutation = "MUL7007";

    /// <summary>Gets the code for disabled array-element mutation.</summary>
    public const string DisabledArrayElementMutation = "MUL7008";

    /// <summary>Gets the code for disabled property removal.</summary>
    public const string DisabledPropertyRemoval = "MUL7009";

    /// <summary>Gets the code for disabled multi-level loop control.</summary>
    public const string DisabledMultiLevelLoopControl = "MUL7011";

    /// <summary>Gets the code for disabled trailing commas.</summary>
    public const string DisabledTrailingCommas = "MUL7012";

    /// <summary>Gets the code for syntax unavailable in the selected language version.</summary>
    public const string UnsupportedLanguageVersionFeature = "MUL7013";
}
