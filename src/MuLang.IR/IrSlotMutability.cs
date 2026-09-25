namespace MuLang.IR;

/// <summary>Specifies whether an IR local slot permits reassignment.</summary>
public enum IrSlotMutability
{
    /// <summary>Permits any validated instruction to define the slot.</summary>
    Mutable = 0,

    /// <summary>Permits exactly one syntactic definition site.</summary>
    ReadOnly = 1,
}
