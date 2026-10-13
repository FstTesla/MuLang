namespace MuLang.IR.Serialization;

/// <summary>Represents options applied while writing MuIR.</summary>
public sealed class MuIrWriterOptions
{
    /// <summary>Gets the default writer options.</summary>
    public static MuIrWriterOptions Default { get; } = new ();

    /// <summary>Gets a value indicating whether diagnostic attributes are written.</summary>
    public bool WriteAttributes { get; init; } = true;

    /// <summary>Gets a value indicating whether the source line-map attribute is written.</summary>
    /// <remarks>This option has no effect when <see cref="WriteAttributes" /> is <c>false</c>.</remarks>
    public bool WriteSourceLineMap { get; init; } = true;
}
