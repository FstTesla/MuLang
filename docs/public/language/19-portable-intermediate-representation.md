# 19. Portable intermediate representation

The compiler MUST lower validated source into a runtime-independent typed intermediate representation before export.

When enabled by the selected language profile, the compiler MUST perform compile-time constant evaluation as defined in [Section 10.11](10-expressions.md#1011-compile-time-constant-evaluation) after binding and before lowering. Folded expressions are represented by ordinary typed IR constants; the IR does not require a distinct constant-expression form.

The portable IR compilation unit SHOULD contain:

- compilation mode metadata;
- the language-profile fingerprint;
- the environment fingerprint;
- a distinguished top-level entry function;
- zero or more user-defined functions;
- independent parameter, local, and temporary slot spaces for each function;

- constants;
- local slots;
- basic blocks;
- typed load and store instructions;
- global reads;
- property and array operations;
- intrinsic array-length reads;
- property removal;
- provider calls by stable symbolic identifier;
- user-defined calls by compiler-assigned stable identifier;
- implicit and contextual conversions;
- checked casts;
- explicit null tests and branches for null-coalescing evaluation;
- truthiness normalization from one non-void source slot to one `bool` destination slot;
- branches and jumps;
- returns;
- source-span associations.

The IR MUST encode evaluation order and short-circuit behavior explicitly.

The IR validator MUST require a truthiness-normalization destination to have type `bool`, its source to have a non-void type, both slots to exist, and the source to be definitely defined.

Array types transported through IR retain their read-only capability. Array creation records that capability, mutable-to-read-only conversions are representation-preserving, acquisition of mutable capability requires a checked conversion, and `SetElement` MUST be rejected when the target slot has a read-only array type.

Conversion instructions use an explicit conversion kind that distinguishes checked casts from value conversions. Checked casts validate the same runtime conformance relation as type-test instructions and return the original runtime value unchanged. Value conversions may change representation where the language specifies an implicit, contextual, or compiler-required conversion. Statically guaranteed casts SHOULD lower directly to a typed copy rather than a conversion instruction.

Provider-call validation accepts arguments assignable through read-only array covariance.

Runtime exporters MUST NOT perform name resolution, type inference, overload resolution, or high-level control-flow interpretation.

Portable IR MAY be persisted and exchanged as a MuIR document with the `.muir` extension. MuIR is versioned independently from the MuLang language and profile versions. A host that reads MuIR MUST validate the reconstructed program against its selected environment through the ordinary IR validator before export or execution.

The normative MuIR version 1 format is defined in [Section 22](22-muir-format.md).
