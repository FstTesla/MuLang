namespace MuLang.Core.Runtime;

/// <summary>Defines categories of MuLang runtime errors.</summary>
public enum RuntimeErrorCategory
{
    /// <summary>An intrinsic operation failed.</summary>
    Operation,

    /// <summary>An object or array mutation failed.</summary>
    Mutation,

    /// <summary>An application-defined operation failed.</summary>
    Application,

    /// <summary>A provider implementation failed unexpectedly.</summary>
    Provider,

    /// <summary>An execution resource limit was exhausted.</summary>
    Resource,

    /// <summary>Execution was cancelled.</summary>
    Cancellation,

    /// <summary>The runtime environment was incompatible or incomplete.</summary>
    Environment,

    /// <summary>A runtime value violated a declared contract.</summary>
    RuntimeContract,
}
