# MuIR Serialization Implementation Plan

## Goal

Implement a simple, deterministic, pseudo-human-readable textual format for portable MuLang IR.

The format:

- uses the `.muir` extension;
- is named MuIR;
- supports serialization and deserialization of `IrProgram`;
- preserves all information currently represented by the public IR model;
- is independent from .NET runtime type names and serializer-specific metadata;
- can be inspected and reasonably edited by a person;
- has an explicit format version;
- produces stable output for the same IR graph;
- reconstructs an IR graph that can be validated through the existing `IrValidator`.

MuIR is a transport and persistence format for portable IR. It is not a source language, an alternate compiler frontend, or a replacement for IR validation.

## Scope

The first version covers:

- program metadata;
- entry and user-defined functions;
- slots;
- basic blocks;
- every current instruction and terminator;
- source spans;
- intrinsic, nullable, array, and structured object types;
- supported IR constant values;
- precise parse diagnostics;
- canonical serialization;
- round-trip tests;
- format-version rejection;
- defensive parsing limits.

The first version does not include:

- binary encoding;
- compression;
- encryption or signing;
- embedded source text;
- embedded environment declarations or provider implementations;
- automatic environment reconstruction;
- migration between incompatible MuIR versions;
- preservation of comments or non-canonical whitespace;
- asynchronous APIs unless a demonstrated host requirement justifies them.

## Package and dependency placement

Implement the format in the existing `MuLang.IR` project under the `MuLang.IR.Serialization` namespace.

This keeps the dependency graph unchanged:

- `MuLang.IR` continues to depend only on `MuLang.Core`;
- `MuLang.Compiler` can serialize compilation output without a new package;
- exporters can deserialize MuIR by referencing the package they already consume;
- no JSON or third-party serialization dependency is introduced.

Add implementation files under `src/MuLang.IR/Serialization` and tests under `tests/MuLang.IR.Tests/Serialization`.

## Format principles

### Textual and line-oriented

MuIR v1 is UTF-8 text. The canonical writer uses UTF-8 without a byte-order mark and `\n` as the wire-format line ending, independently of the host operating system.

The document is organized into explicit sections. Metadata and declarations occupy one line where practical. Functions and blocks use braces and indentation. Each instruction and terminator occupies one line.

The reader accepts:

- `\n` and `\r\n`;
- insignificant horizontal whitespace between tokens;
- blank lines;
- comments beginning with `#` outside string literals.

The writer emits:

- two-space indentation;
- one space between ordinary tokens;
- no comments;
- no trailing whitespace;
- one blank line between top-level function declarations;
- one final newline.

### Explicit versioning

Every document starts with a mandatory magic and format version:

- magic: `muir`;
- version: positive decimal integer;
- initial version: `1`.

The reader rejects:

- missing or invalid magic;
- version zero;
- unsupported versions;
- trailing data after the complete document.

Version numbers describe the serialized format, not the MuLang language version. The language-profile fingerprint remains separate metadata.

### Stable textual tokens

Do not serialize .NET enum names, numeric enum values, record type names, assembly-qualified names, or reflection metadata.

Define explicit, case-sensitive wire tokens for:

- compilation modes;
- slot kinds;
- unary operators;
- binary operators;
- conversion kinds;
- instruction opcodes;
- terminators;
- type constructors;
- constant representations.

Token mappings are part of the MuIR compatibility contract and must be tested exhaustively.

### Canonical output

The writer preserves the declared order of:

- user functions;
- slots;
- basic blocks;
- instructions;
- array elements;
- object value properties.

Structured type properties are emitted in their existing declared order. The writer does not silently normalize the IR graph or change semantically observable collection order.

Type-table identifiers are assigned by deterministic first occurrence while traversing:

1. entry function return type;
2. entry function slots and instructions;
3. user functions in declared order;
4. each user function's return type, slots, and instructions;
5. nested type components in declaration order.

Serializing the same graph twice must produce byte-identical output.

## Document structure

MuIR v1 contains the following sections in fixed order:

1. magic and format version;
2. program metadata;
3. type table;
4. entry function;
5. user functions;
6. explicit end of document.

Fixed section ordering simplifies the parser, prevents ambiguous recovery, and makes diffs predictable.

### Program metadata

Persist:

- compilation mode;
- environment fingerprint;
- language-profile fingerprint.

Fingerprints are quoted strings and are reconstructed through their public constructors. The reader reports invalid empty or whitespace-only values as format diagnostics rather than leaking constructor exceptions.

### References and identifiers

Use dedicated prefixes for local wire references:

- type references: `t` followed by a non-negative decimal identifier;
- slot references: `%` followed by a non-negative decimal identifier;
- block references: `bb` followed by a non-negative decimal identifier.

Function IDs, provider symbol IDs, source names, property names, type names, type IDs, and fingerprints are quoted strings.

String escaping follows a small format-owned rule set:

- quote;
- backslash;
- newline;
- carriage return;
- tab;
- Unicode scalar escape.

The reader rejects invalid escapes, unpaired surrogates, and invalid UTF-8.

### Source spans

Every instruction and terminator carries its existing `TextSpan`.

Encode spans as two non-negative decimal integers:

- start;
- length.

The reader uses checked arithmetic and rejects spans whose end would exceed `int.MaxValue`.

Spans continue to refer to the original MuLang source. Parse diagnostics for the `.muir` document use separate document offsets and line/column information.

## Type table

Emit every referenced type once and refer to it by type-table identifier elsewhere in the document.

The type table supports:

- `bool`;
- `int`;
- `float`;
- `number`;
- `string`;
- `unknown`;
- `object`;
- `void`;
- internal `null` where present in a representable IR graph;
- nullable types;
- mutable arrays;
- read-only arrays;
- named structured object types;
- anonymous structured object types.

Do not serialize `TypeKind.Error`. The writer rejects it because it represents compiler recovery rather than portable executable IR.

### Composite types

Nullable type definitions contain one underlying type reference.

Array type definitions contain:

- one element type reference;
- an explicit mutable or read-only capability token.

Structured object type definitions contain:

- optional provider type ID;
- language-facing name;
- open or closed marker;
- ordered property declarations.

Each property declaration contains:

- quoted property name;
- property type reference;
- required or optional marker.

The reader reconstructs:

- provider types through the public `ObjectTypeSymbol` constructor;
- anonymous types through `ObjectTypeSymbol.CreateAnonymous`;
- nullable and array types through `TypeSymbols` factories.

Type definitions may refer only to previously declared type identifiers in MuIR v1. The writer therefore emits dependencies before dependants. This avoids placeholders and partially initialized type objects.

The reader reports duplicate type identifiers, undefined references, invalid composites, and invalid property declarations as format diagnostics.

## Constants

The writer supports the portable constant domain accepted by compiler-produced IR:

- `null`;
- `bool`;
- signed 64-bit integer;
- binary64 floating-point value;
- string.

The constant encoding includes an explicit value-kind token. Do not infer the runtime representation from the destination type because `number` and `unknown` may contain either integer or floating-point values.

Integer values use invariant decimal notation.

Finite floating-point values use invariant round-trip notation. Define dedicated tokens for:

- positive infinity;
- negative infinity;
- NaN;
- negative zero.

The reader must reproduce the same ordinary finite `double` value and preserve negative zero. MuIR v1 does not preserve NaN payload bits because MuLang does not expose them semantically.

Although `IrInstruction.Constant.Value` is typed as `object?`, arbitrary host objects are not portable. The writer rejects unsupported runtime values with a serialization exception identifying the function, block, and instruction.

After materialization, constant compatibility remains subject to `IrValidator`.

## Functions, slots, and blocks

### Functions

Each function declaration contains:

- quoted function ID;
- return type reference;
- entry block reference;
- slot declarations;
- block declarations.

The entry function is marked explicitly. User functions use the same body grammar and appear in `IrProgram.UserFunctions` order.

### Slots

Each slot declaration contains:

- slot ID;
- explicit slot-kind token;
- type reference;
- optional quoted source-level name.

Absence of a name uses a dedicated `none` token rather than an empty string.

### Basic blocks

Each block declaration contains:

- block ID;
- zero or more instructions;
- exactly one terminator.

The reader treats a missing or duplicate terminator as a format error. Control-flow validity, definite assignment, slot compatibility, and target existence remain the responsibility of `IrValidator`.

## Instruction encoding

Define one stable lowercase, hyphen-separated opcode for every `IrInstruction` subtype:

| IR instruction | MuIR opcode |
|---|---|
| `Constant` | `constant` |
| `Copy` | `copy` |
| `LoadGlobal` | `load-global` |
| `Unary` | `unary` |
| `Binary` | `binary` |
| `Convert` | `convert` |
| `Truthiness` | `truthiness` |
| `TypeTest` | `type-test` |
| `IsNull` | `is-null` |
| `HasProperty` | `has-property` |
| `CreateArray` | `create-array` |
| `CreateObject` | `create-object` |
| `GetProperty` | `get-property` |
| `SetProperty` | `set-property` |
| `RemoveProperty` | `remove-property` |
| `GetElement` | `get-element` |
| `SetElement` | `set-element` |
| `RemoveElementProperty` | `remove-element-property` |
| `ProviderCall` | `provider-call` |
| `UserCall` | `user-call` |

Each opcode has a fixed operand count and operand order. Encode optional destination slots with `none`. Encode Boolean flags with `true` and `false`.

Collection operands use delimited lists:

- array element slots;
- object property values;
- provider-call arguments;
- user-call arguments.

Object property values retain their declared order and contain a quoted name plus a slot reference.

The parser dispatches directly by opcode and reports unknown opcodes without attempting reflection-based construction.

## Terminator encoding

Define stable tokens for:

| IR terminator | MuIR token |
|---|---|
| `Jump` | `jump` |
| `Branch` | `branch` |
| `Return` | `return` |

Return without a value uses the `none` token.

## Public API

Introduce a small API surface in `MuLang.IR.Serialization`.

### Writing

Provide `MuIrWriter` entry points for:

- serializing to a `TextWriter`;
- serializing to a `Stream` as canonical UTF-8;
- serializing to a string for diagnostics and tests.

Writing APIs:

- require a non-null `IrProgram`;
- leave caller-owned writers and streams open by default;
- expose an explicit `leaveOpen` choice where the API creates an intermediate writer;
- throw `MuIrSerializationException` for representability failures;
- allow ordinary I/O exceptions to propagate.

MuIR v1 has no formatting options. A single canonical writer avoids compatibility differences between option combinations.

### Reading

Provide `MuIrReader` entry points for:

- reading from a `TextReader`;
- reading UTF-8 from a `Stream`;
- reading from a string.

Return `MuIrReadResult` containing:

- `IrProgram? Program`;
- an immutable ordered collection of `MuIrDiagnostic`;
- a success indicator derived from the absence of error diagnostics.

Return no program when any lexical, syntactic, reference-resolution, or materialization error occurs. Do not expose a partially constructed IR graph.

`MuIrDiagnostic` contains:

- stable diagnostic code;
- severity;
- zero-based document offset and length;
- one-based line and column;
- message.

Malformed content is reported through the result rather than ordinary exceptions. Null arguments, disposed streams, I/O failures, and programmer misuse continue to throw.

### Semantic validation

Deserialization reconstructs the IR but does not reconstruct an `EnvironmentSchema`.

The documented consumption flow is:

1. read the MuIR document;
2. require a successful `MuIrReadResult`;
3. supply the host-selected environment;
4. call `IrValidator.Validate`;
5. export only when validation succeeds.

Do not duplicate environment resolution, provider lookup, CFG checks, or type checking in the MuIR reader.

Consider a convenience API that accepts an `EnvironmentSchema` only after the basic reader is complete. It must delegate to `IrValidator` and must not introduce a second validation implementation.

## Internal implementation

### Lexer

Implement a dedicated lexer over `ReadOnlyMemory<char>` or an equivalent buffered abstraction.

It recognizes:

- keywords and wire tokens;
- decimal integers;
- punctuation;
- quoted strings;
- comments;
- end of document.

Track document offset, line, and column while lexing. Preserve token spans for diagnostics.

Do not use regular expressions for the complete parser and do not deserialize through reflection.

### Parser

Implement a recursive-descent parser with explicit methods for:

- header;
- metadata;
- type table;
- type definitions;
- functions;
- slots;
- blocks;
- instructions;
- terminators;
- spans;
- lists;
- literals.

Use local recovery boundaries at:

- the next top-level section;
- the next function;
- the next block;
- the next instruction or terminator line.

Accumulate useful independent diagnostics, but stop materialization if any error exists.

### Materialization

Separate parsed wire data from public IR construction.

Use internal immutable syntax or DTO records that contain only:

- primitive values;
- token spans;
- unresolved integer references;
- ordered child collections.

Resolve types first, then construct functions and the final `IrProgram`. Catch expected constructor validation failures at the materialization boundary and convert them into MuIR diagnostics. Do not broadly catch unexpected exceptions.

### Writer

Implement the writer without reflection.

Use exhaustive pattern matching over:

- `TypeSymbol` variants;
- `IrInstruction` variants;
- `IrTerminator` variants;
- supported constant runtime values.

Unexpected future variants fail explicitly with `MuIrSerializationException`. Compiler exhaustiveness warnings and tests should make additions visible when the IR model evolves.

## Defensive parsing

Treat `.muir` input as potentially untrusted.

Introduce `MuIrReaderOptions` with conservative defaults for:

- maximum document length;
- maximum string length;
- maximum token length;
- maximum type nesting depth;
- maximum number of types;
- maximum number of functions;
- maximum slots per function;
- maximum blocks per function;
- maximum instructions per block;
- maximum elements in any operand list;
- maximum accumulated diagnostics.

Limits must be configurable upward by trusted hosts. Values must be validated when options are constructed.

Use checked numeric parsing and avoid recursive algorithms whose stack usage is controlled solely by input. Report limit violations as format diagnostics.

## Compatibility policy

MuIR v1 compatibility is based on the textual wire contract, not the current C# model names.

Rules:

- adding a new optional reader behavior does not change canonical v1 output;
- adding an instruction, terminator, type kind, or required field requires a new MuIR version unless it can be represented by an already reserved construct;
- changing an existing token or operand order requires a new version;
- readers reject unsupported versions rather than guessing;
- writers emit only the latest version they explicitly support;
- version-specific readers remain isolated behind a small dispatch layer;
- canonical v1 fixtures remain permanent compatibility tests.

Do not promise forward compatibility with unknown opcodes in v1. Silently ignoring executable IR would be unsafe.

## Diagnostics

Reserve a MuIR-specific diagnostic prefix distinct from compiler and IR validation diagnostics.

Define codes for at least:

- invalid magic;
- unsupported version;
- invalid token;
- unterminated string;
- invalid escape;
- invalid UTF-8;
- missing section;
- duplicate declaration;
- undefined reference;
- invalid numeric value;
- numeric overflow;
- invalid type definition;
- invalid instruction;
- invalid terminator;
- invalid span;
- unexpected token;
- trailing content;
- configured limit exceeded;
- constructor/materialization failure.

Messages should name the offending token or reference and the declaration being parsed where practical.

## Test plan

### Writer coverage

Add focused tests for:

- every instruction subtype;
- every terminator subtype;
- every operator and conversion token;
- every supported type shape;
- mutable and read-only arrays;
- named and anonymous structured objects;
- required and optional object properties;
- every supported constant runtime representation;
- special floating-point values;
- nullable constants;
- escaped strings;
- source spans;
- absent optional destinations, names, and return values.

### Reader coverage

Add focused tests for:

- all valid constructs;
- comments and accepted whitespace;
- both accepted line-ending forms;
- invalid magic and versions;
- malformed strings and escapes;
- duplicate and undefined references;
- invalid composite types;
- invalid enum and opcode tokens;
- malformed lists;
- missing terminators;
- overflowed identifiers and spans;
- unsupported constants;
- each configured resource limit;
- multiple recoverable diagnostics in one document;
- no partially returned program after an error.

### Round-trip coverage

For compiler-produced programs representing expression and program modes:

1. compile source to `IrProgram`;
2. serialize to canonical MuIR;
3. deserialize;
4. validate against the original environment;
5. serialize the reconstructed program again;
6. require byte-identical canonical output;
7. export and execute both programs;
8. require equivalent results and observable provider calls.

Include programs with:

- user functions;
- control-flow branches and loops;
- provider calls;
- conversions and checked casts;
- truthiness normalization;
- object and array operations;
- read-only arrays;
- nullable values;
- property removal;
- short-circuit evaluation.

### Compatibility fixtures

Commit hand-reviewed canonical `.muir` fixtures under the IR test project.

Fixtures cover:

- a minimal expression program;
- a multi-function program;
- the complete v1 instruction set;
- the complete v1 type and constant set.

Tests deserialize every fixture and compare reserialization byte-for-byte. Fixture changes require explicit format-compatibility review.

### Public API checks

Update `PublicAPI.Unshipped.txt` for all new public types and members.

Run:

- `MuLang.IR.Tests`;
- compiler tests that inspect lowered IR;
- .NET exporter tests using at least one deserialize-before-export path;
- repository API compatibility checks.

## Documentation changes

After implementation:

- update `docs/public/language/19-portable-intermediate-representation.md` to state that portable IR can be persisted as MuIR;
- document the `.muir` extension and versioning contract;
- document the read, validate, export lifecycle;
- update `docs/public/packages.md` with the serialization responsibility of `MuLang.IR`;
- add the feature to the detailed changelog;
- publish the complete MuIR v1 grammar or wire specification in a dedicated public document.

Do not make this plan the permanent format specification. Once implemented, move normative details into public documentation and keep tests and fixtures as executable compatibility evidence.

## Implementation sequence

### Phase 1: Freeze MuIR v1

- finalize keywords, punctuation, opcode tokens, and operand order;
- define the normative grammar;
- define canonical whitespace and escaping;
- define the constant domain;
- define diagnostic codes;
- add hand-reviewed fixture drafts.

Do not implement the writer and reader against separate informal assumptions.

### Phase 2: Shared format primitives

- add wire-token mappings;
- add string escaping and unescaping;
- add invariant integer and floating-point codecs;
- add source-location tracking;
- add reader options and diagnostics;
- test every primitive independently.

### Phase 3: Canonical writer

- collect and order the type table;
- write metadata and types;
- write functions, slots, and blocks;
- implement every instruction and terminator;
- reject unsupported types and constant values explicitly;
- add snapshot and determinism tests.

Completing the writer first provides canonical test inputs for the reader.

### Phase 4: Reader and materializer

- implement lexer;
- implement parser and recovery;
- parse into internal wire records;
- resolve type and IR references;
- construct the public IR graph;
- enforce configured limits;
- add malformed-input and diagnostic-location tests.

### Phase 5: End-to-end validation

- add compile/write/read/validate/export tests;
- verify canonical reserialization;
- verify language-profile and environment fingerprints survive exactly;
- verify all existing exporter behavior after deserialization;
- run API compatibility checks.

### Phase 6: Public documentation

- publish the MuIR v1 specification;
- update IR and package documentation;
- update the changelog;
- mark fixture files as compatibility artifacts.

## Acceptance criteria

The implementation is complete when:

- every public IR node has an explicit MuIR v1 encoding;
- every compiler-produced valid `IrProgram` using supported constants can be serialized;
- every canonical MuIR v1 document can be deserialized without host-specific type metadata;
- deserialized programs validate through the existing `IrValidator`;
- serialize-read-serialize produces byte-identical output;
- deserialized programs export and execute equivalently to their in-memory originals;
- unsupported constants and future unknown IR nodes fail explicitly;
- malformed or oversized input produces bounded, location-aware diagnostics;
- unsupported versions are rejected;
- no third-party serialization dependency is added;
- public API baselines, tests, documentation, and compatibility fixtures are updated.
