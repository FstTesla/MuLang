namespace MuLang.IR.Serialization;

/// <summary>Specifies the grammatical role of a MuIR identifier occurrence.</summary>
public enum MuIrOccurrenceRole
{
    /// <summary>Identifies a declaration.</summary>
    Declaration,

    /// <summary>Identifies a slot read.</summary>
    Read,

    /// <summary>Identifies a slot definition.</summary>
    Write,

    /// <summary>Identifies a structural reference.</summary>
    Reference,
}
