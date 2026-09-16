namespace MuLang.Core;

/// <summary>Specifies how values are interpreted in conditions.</summary>
public enum ConditionSemantics
{
    /// <summary>Requires condition expressions to have the Boolean type.</summary>
    StrictBoolean = 0,

    /// <summary>Evaluates condition expressions using MuLang truthiness rules.</summary>
    Truthiness = 1,
}
