namespace MuLang.Compiler.Diagnostics;

internal static class DiagnosticCodes
{
    public const string InvalidCharacter = "MUL1001";
    public const string UnterminatedString = "MUL1002";
    public const string InvalidEscapeSequence = "MUL1003";
    public const string InvalidNumber = "MUL1004";
    public const string UnexpectedToken = "MUL2001";
    public const string ExpectedExpression = "MUL2002";
    public const string InvalidExpressionStatement = "MUL2003";
    public const string InvalidAssignmentTarget = "MUL2004";
    public const string InvalidRemovalTarget = "MUL2005";
    public const string NonAssociativeOperator = "MUL2006";
    public const string InvalidForClause = "MUL2007";
    public const string TrailingSeparator = "MUL2008";
    public const string RepeatedNullableAnnotation = "MUL2009";
}
