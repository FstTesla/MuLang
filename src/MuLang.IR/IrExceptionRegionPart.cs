namespace MuLang.IR;

/// <summary>Identifies a component of an exception region.</summary>
public enum IrExceptionRegionPart
{
    /// <summary>Identifies the protected component.</summary>
    Protected,

    /// <summary>Identifies the handler component.</summary>
    Handler,

    /// <summary>Identifies the cleanup component.</summary>
    Cleanup,
}
