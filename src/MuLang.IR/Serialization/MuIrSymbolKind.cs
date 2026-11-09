namespace MuLang.IR.Serialization;

/// <summary>Specifies the grammatical kind of a MuIR identifier.</summary>
public enum MuIrSymbolKind
{
    /// <summary>Identifies a wire type.</summary>
    Type,
    /// <summary>Identifies a function.</summary>
    Function,
    /// <summary>Identifies a function-local slot.</summary>
    Slot,
    /// <summary>Identifies a function-local block.</summary>
    Block,
    /// <summary>Identifies an explicitly declared lifetime region.</summary>
    LifetimeRegion,
    /// <summary>Identifies an explicitly declared exception region.</summary>
    ExceptionRegion,
}
