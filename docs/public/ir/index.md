# Intermediate representation

This section specifies MuLang's portable intermediate representation and its canonical textual serialization format.

- Read the [portable intermediate representation specification](portable-intermediate-representation.md).
- Read the [MuIR serialization format specification](muir-format.md).

## Shared document analysis

`MuIrReader.Analyze(text, options)` performs the shared reader analysis once.
Its `MuIrAnalysisResult.ReadResult` contains the reconstructed program and the
ordinary reader diagnostics. `Document` is available only after complete parsing
and materialization succeed; failures expose no partial syntax projection.
The existing `Read` overloads and canonical writer format are unchanged.

`MuIrDocument` retains the supported format version, all wire types in ID order,
decoded tokens, grammatical identifier occurrences, declaration selection and
complete ranges, and delimited structural ranges. `Types` includes unused types
and separate IDs for structurally equivalent declarations. Occurrence roles
distinguish declarations, slot reads, slot definitions, and structural references.
Known attribute targets retain their grammatical scope; unknown payloads are
retained as tokens without deriving symbols from their spelling.

`MuIrRange` offsets and lengths count UTF-16 code units in the MuIR document.
They are not `TextSpan` offsets in the original MuLang source. Consumers using
scalar-indexed `SourceText` must convert document offsets before producing spans.
Serialized source locations and diagnostic names remain optional metadata.

This analysis is for reading and editor consultation. It does not bind provider
symbols or replace the `IrValidator` lifecycle required before execution or export.
