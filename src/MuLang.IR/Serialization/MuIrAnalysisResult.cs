namespace MuLang.IR.Serialization;

/// <summary>Represents the shared parsing and materialization outcome of a MuIR document.</summary>
public sealed class MuIrAnalysisResult
{
    internal MuIrAnalysisResult(MuIrReadResult readResult, MuIrDocument? document)
    {
        ReadResult = readResult;
        Document = document;
    }

    /// <summary>Gets the reconstructed program and reader diagnostics.</summary>
    public MuIrReadResult ReadResult { get; }

    /// <summary>Gets the parsed document, or <c>null</c> unless complete reading and materialization succeeded.</summary>
    public MuIrDocument? Document { get; }
}
