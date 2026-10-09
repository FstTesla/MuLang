# Well-known IR attributes plan

## Status

Planned for MuIR version 2 and package release `0.3.0`.

Stable package release `0.2.0` used MuIR 1; MuIR 2 exists only in the
`0.3.0` prerelease line. Adding attributes to MuIR 2 therefore does not require
a new format version, while MuIR 1 remains the stable compatibility format.

The plan records the following decisions:

- persist attributes in MuIR version 2 rather than in a sidecar file;
- continue reading MuIR version 1;
- accept existing MuIR 2 prerelease documents without an attribute section;
- ignore unknown well-known attributes when their payload is correctly
  delimited;
- initially support program, function, slot, and type targets;
- deprecate the positional `IrSlot.Name` representation in favor of a
  well-known slot attribute;
- keep the attribute system closed and standardized rather than exposing
  arbitrary producer-defined metadata.

Instruction, terminator, block, lifetime-region, and exception-region targets
are intentionally deferred.

## Motivation

Portable IR preserves the information required for validation and execution,
but deliberately erases most source-level names:

- user functions retain compiler-assigned IDs such as `user:0`, not their
  source names;
- provider globals and functions retain stable provider IDs, not necessarily
  their language-facing names;
- structured types become purely structural and lose provider IDs,
  language-facing names, and named-versus-anonymous origin;
- slots currently preserve an optional name through a dedicated positional
  field, but no general mechanism exists for other diagnostic information.

Stable IDs are correct for execution, environment compatibility, and
serialization. They are less useful in runtime diagnostics, stack traces, IR
dumps, and host-facing error reports.

The IR therefore needs an optional diagnostic layer that preserves selected
well-known source information without making it semantic and without opening a
general metadata facility.

## Goals

- Preserve useful source names for runtime diagnostics and tooling.
- Keep every attribute optional.
- Define a closed catalog of standardized attribute names, targets, payloads,
  and cardinalities.
- Ensure that removing all attributes leaves an executable program with the
  same validation and runtime behavior.
- Keep source names out of type identity, assignability, environment
  compatibility, and executable instruction semantics.
- Preserve deterministic canonical MuIR output for attributes understood by
  the writer.
- Allow a MuIR 2 reader to skip a future well-known attribute without
  rejecting the complete document.
- Continue reading MuIR 1 documents and project their legacy slot names into
  the new model.
- Provide bounded parsing and validation for attribute collections and
  payloads.
- Give exporters and runtime-diagnostic producers a shared API for resolving a
  diagnostic display name with a stable-ID fallback.

## Non-goals

- Do not introduce arbitrary key-value metadata.
- Do not permit custom namespaces or producer-defined attribute names.
- Do not make source names observable to MuLang programs.
- Do not require attributes for execution or validation.
- Do not restore nominal type identity.
- Do not preserve complete syntax trees, symbol tables, lexical scopes, or
  debugger state.
- Do not add reflection over functions, slots, globals, or types.
- Do not assign persistent IDs to instructions or terminators in this version.
- Do not make a MuIR 1 reader accept a MuIR 2 document.
- Do not preserve unknown attribute payloads when a document is read and
  rewritten by a reader that does not understand them.

## Semantic invariants

Attributes are descriptive and non-semantic.

The following operations MUST produce the same result whether the attributes
are present or absent:

- structural IR validation, except for validation of the attribute table
  itself;
- provider-environment compatibility validation;
- type equivalence and assignability;
- control-flow and definite-assignment analysis;
- exporter selection and executable behavior;
- runtime value conformance;
- execution limits and cancellation;
- provider and user-function dispatch.

An exporter or runtime MAY use an attribute to improve a diagnostic message.
It MUST fall back to the existing stable identifier or structural type display
when the attribute is absent, unknown, malformed before materialization, or
discarded by an older MuIR 2 reader.

Attributes MUST NOT participate in canonical type identity. In particular,
two structurally equivalent object types remain one canonical MuIR type entry
even when they originated from differently named source or provider types.

## Initial target model

### Program target

The program target describes the complete compilation unit.

The initial catalog uses it for:

- an optional source-document name or URI;
- language-facing names associated with referenced provider global IDs;
- language-facing names associated with referenced provider function IDs.

Provider globals and functions do not have standalone IR declarations. A
program-level mapping avoids adding instruction targets solely to recover
their names.

Only referenced provider symbols SHOULD be included. This keeps attributes
proportional to the executable IR and avoids serializing unrelated environment
declarations.

### Function target

A function target identifies either:

- the distinguished entry function;
- a user function by its portable function ID.

The function ID remains authoritative for calls and runtime dispatch. A source
name or qualified source name is diagnostic only.

Current MuLang permits only top-level user functions. Qualified names are
reserved for hosts, future source organization, or a future nested-function
feature; the compiler does not emit a redundant qualified name while it would
be identical to the simple source name.

### Slot target

A slot target consists of:

- its containing function target;
- its function-local slot ID.

Source names are useful for parameters and local slots. Compiler temporaries
normally have no source name and SHOULD NOT receive one.

The target does not change slot lifetime or visibility. A slot attribute
describes every semantic activation of that slot ID.

### Type target

An in-memory type target refers to the projected portable `TypeSymbol`. Its
wire representation refers to the corresponding type-table ID.

A canonical structural type may represent several named source or provider
types. The type attribute therefore contains an ordered set of source names,
not one authoritative name.

Anonymous compiler-generated names such as `<anonymous>` MUST NOT be emitted.
Names are normalized by ordinal comparison, deduplicated, and serialized in
ordinal order.

## Initial well-known catalog

| Attribute | Target | Payload | Cardinality |
|---|---|---|---|
| `source-document` | program | one string | zero or one |
| `provider-global-name` | program | provider ID and language-facing name | zero or more, unique by provider ID |
| `provider-function-name` | program | provider ID and language-facing name | zero or more, unique by provider ID |
| `source-name` | function or slot | one string | zero or one per target |
| `qualified-source-name` | function | one string | zero or one |
| `declaration-span` | function or slot | one source span | zero or one per target |
| `source-type-names` | type | a non-empty string list | zero or one |

The first implementation MAY omit `source-document` and declaration spans if
the compiler does not yet have a stable document identity or exact declaration
span at the relevant lowering boundary. The model and wire definitions should
still reserve their standard payloads so adding compiler population does not
require another format version.

Names are Unicode strings and use the ordinary MuIR string rules. The reader
MUST reject empty or whitespace-only values for source names, qualified names,
provider IDs, and provider language names.

The catalog belongs to the MuIR specification. Adding another attribute for an
existing target and using a self-delimited payload does not require MuIR 4.
Adding a target kind or changing the meaning or payload of an existing
attribute does require a new format version.

## Public IR API

### Attribute collection

Add an immutable `IrAttributeCollection` in `MuLang.IR`.

The collection exposes typed lookup rather than a writable string dictionary.
It contains:

- optional program attributes;
- function attributes keyed by portable function identity;
- slot attributes keyed by function identity and slot ID;
- type attributes keyed by projected `TypeSymbol`.

Use reference identity for in-memory type keys after IR type projection. The
projection step already canonicalizes structurally equivalent portable types
to shared projected instances.

Expose read-only typed records:

- `IrProgramAttributes`;
- `IrFunctionAttributes`;
- `IrSlotAttributes`;
- `IrTypeAttributes`;
- `IrProviderSymbolName`.

The public model MUST NOT expose unknown attributes or raw payload tokens.
Unknown attributes accepted by the reader are skipped and discarded.

Add `IrAttributeCollection.Empty` and make empty collections reusable.

### Program integration

Add an `Attributes` init-only property to `IrProgram`, defaulting to
`IrAttributeCollection.Empty`.

Keep the existing positional constructor source-compatible. Existing callers
that construct `IrProgram` without attributes continue to produce a valid
program.

Program record equality is not the definition of executable or wire semantic
identity. Document that attributes are diagnostic even though they are
reachable from the program object.

Every transformation that creates a replacement `IrProgram`, including
`IrTypeProjector`, MUST explicitly preserve or project the attribute
collection. Tests must detect accidental attribute loss.

### Diagnostic-name resolver

Add one shared helper in `MuLang.IR` that resolves:

- a function display name from a function target;
- a slot display name from a slot target;
- a type diagnostic name or alias list;
- a provider symbol display name from its stable ID.

The helper always accepts the existing stable or structural fallback. Runtime
and exporter components should not duplicate lookup and fallback rules.

### `IrSlot.Name` migration

Keep `IrSlot.Name` for source and binary compatibility during the MuIR 2
transition, but mark it obsolete in favor of program attributes.

The compiler and new tests should stop reading `IrSlot.Name` directly.

Compatibility behavior is:

- reading MuIR 1 or legacy MuIR 2 converts a non-`none` positional slot name
  into a `source-name` slot attribute and also materializes `IrSlot.Name`;
- reading MuIR 2 materializes `IrSlot.Name` from `source-name` when present so
  existing consumers continue to work;
- writing canonical MuIR 2 serializes the attribute and writes `none` for the
  positional slot-name operand retained by the grammar;
- when an API caller supplies only `IrSlot.Name`, the writer synthesizes the
  equivalent `source-name` attribute;
- when both representations are present, they MUST match; otherwise, writing
  fails explicitly.

Removing `IrSlot.Name` is deferred to a future breaking public-API release and
does not itself require a MuIR format change.

## Compiler production

### Attribute builder

Add an internal typed `IrAttributeBuilder` to the compiler lowering layer.

The builder tracks the current portable function ID and records attributes as
symbols are lowered:

- user-function source name when lowering `BoundFunction`;
- parameter source name after its slot ID is allocated;
- local source name after its slot ID is allocated;
- referenced provider global ID and language-facing name when lowering
  `BoundExpression.Global`;
- referenced provider function ID and language-facing name when lowering a
  provider call.

The entry function does not require a source name unless a host-facing
compilation-unit name is available. `$entry` remains its stable fallback.

Compiler temporaries do not receive names.

`IrBuilder` remains responsible for executable structure. It should not gain
symbol or syntax dependencies merely to produce attributes. `Lowerer` owns the
attribute builder because it already has access to bound symbols, source
spans, provider symbols, and allocated slot IDs.

### Type projection and name merging

`IrTypeProjector` currently replaces every structured type with an anonymous
portable structural type. Extend this pass to project type attributes together
with executable types.

For every original structured type:

1. project its structural graph as today;
2. determine whether it has a meaningful language-facing name;
3. associate that name with the projected type result;
4. merge names when several original types map to the same projected type;
5. remove duplicates and sort names ordinally.

Provider IDs remain erased. Only their language-facing type names are retained
as diagnostic aliases.

When source-defined object types are implemented according to
`user-defined-object-types.plan.md`, their names enter the same collection.
The attribute mechanism does not distinguish source-defined and
provider-defined aliases because neither is identity-bearing after projection.

The projection result must carry both:

- the projected `IrProgram`;
- the projected and merged `IrAttributeCollection`.

No name may influence structural graph construction, strongly connected
component ordering, or type-table ID assignment.

## MuIR version 2 attributes

### Document structure

Canonical MuIR 2 adds one required `attributes` section after all user
functions and before `end`. The section is present even when its count is zero.
For compatibility with existing MuIR 2 prerelease documents, readers also
accept the earlier layout without an `attributes` section and convert any
positional slot names into attributes.

The high-level grammar is:

```text
document ::=
  "muir" "2"
  "mode" compilation-mode
  "environment" string
  "profile" string
  "types" integer type-definition*
  entry-function
  "users" integer user-function*
  "attributes" integer "{"
    attribute*
  "}"
  "end"

attribute ::=
  "attribute" attribute-target attribute-name
  "{" attribute-payload-token* "}"

attribute-target ::=
    "program"
  | "function" function-target
  | "slot" function-target slot-ref
  | "type" type-ref

function-target ::=
    "entry"
  | "user" string

attribute-name ::= atom
```

The target is outside the payload and has a fixed MuIR 2 grammar. The payload
is enclosed in balanced braces. A reader recognizes strings and nested
delimiters while skipping an unknown payload, so braces inside strings do not
end it.

Known attributes use these payloads:

```text
source-document-payload       ::= string
provider-symbol-name-payload  ::= string string
source-name-payload           ::= string
qualified-name-payload        ::= string
declaration-span-payload      ::= span
source-type-names-payload     ::= integer "[" (string ("," string)*)? "]"
```

The declared list count MUST match the number of source type names.

### Slot grammar

MuIR 2 retains its positional slot-name operand for wire compatibility, but
canonical writers emit `none` there and serialize names only as attributes.
MuIR 1 retains its existing grammar and reader behavior.

### Canonical form

The canonical MuIR 2 writer:

- always emits version 2;
- always emits the `attributes` section;
- emits `none` for positional slot names;
- omits absent attributes;
- emits targets in program, function, slot, then type order;
- orders functions by entry first and user-function document order;
- orders slots by containing function and slot ID;
- orders types by type-table ID;
- orders attributes for one target by attribute name;
- orders provider-symbol mappings by provider ID;
- orders source type names ordinally;
- rejects duplicate known attributes and conflicting legacy slot names;
- writes payloads with the ordinary canonical string, integer, list, and span
  rules.

The type table MUST be built before serializing type attributes so in-memory
type targets resolve to canonical `tN` references.

Unknown attributes are not represented by the public model and therefore
cannot be emitted by the current writer.

### Compatibility

The reader accepts versions 1 and 2.

For version 1:

- no `attributes` section is expected;
- legacy slot names are converted into attributes;
- all other attribute collections are empty.

For version 2:

- the `attributes` section is required in canonical documents and optional in
  existing prerelease documents;
- positional slot names in legacy documents are converted into attributes;
- canonical documents use `none` for positional slot names;
- known attributes are parsed and validated;
- an unknown attribute name on a known target is skipped and discarded;
- an unknown target kind is rejected because the reader cannot establish its
  reference grammar;
- malformed target references, unbalanced payloads, invalid known payloads,
  duplicate singleton attributes, and invalid cardinality are rejected.

Ignoring an unknown attribute intentionally weakens byte-for-byte round-trip
for documents produced by a newer MuIR 2 writer: an older MuIR 2 reader can
successfully consume such a document, but rewriting it drops attributes that
it did not understand. Canonical byte identity remains required when every
attribute in the input is understood.

A MuIR 1 reader continues to reject version 2 through the existing
unsupported-version behavior. This is the expected forward-compatibility
boundary.

### Reader limits

Extend `MuIrReaderOptions` with positive limits for:

- total attributes;
- attributes per target;
- skipped unknown-payload tokens;
- source type names per type;
- provider-symbol name mappings.

Existing document, string, token, and list-element limits continue to apply.

Skipping an unknown attribute MUST remain bounded by the document, token, and
unknown-payload limits. The parser must not allocate a raw copy of the skipped
payload.

## Attribute validation

Reading validates wire structure. `IrValidator` additionally validates
attribute references on programs created directly through the public API.

Validation requires:

- every function target to resolve to exactly one entry or user function;
- every slot target to resolve within its function;
- every type target to occur in the executable program's projected type graph;
- singleton attributes to occur at most once;
- provider IDs to be unique within each provider-symbol attribute kind;
- source type name lists to be non-empty, unique, and ordinally sorted in
  canonical materialized form;
- every string constrained as a name or ID to be non-empty and non-whitespace;
- declaration spans to satisfy the ordinary checked span constraints.

Attribute validation diagnostics use dedicated IR diagnostic codes and do not
reuse executable instruction or environment-mismatch codes.

Missing attributes are always valid.

## Runtime and exporter integration

### Function stack frames

`RuntimeStackFrame.FunctionId` remains unchanged and authoritative.

Runtime-error formatting resolves an optional source name through the compiled
program's attribute collection. A stack frame can therefore display the
source name while retaining the portable ID for unambiguous host processing.

Do not replace `FunctionId` with a display name and do not make stack-frame
construction fail when attributes are unavailable.

### Slots and types

Runtime diagnostics that already identify a slot or static type MAY use:

- the slot source name instead of only `%N`;
- the first type source name as the primary display name;
- the remaining type names as aliases when useful;
- the existing slot reference or structural display as fallback.

The initial implementation need not expose local-variable values or build a
debugger. It only centralizes names that a diagnostic already has enough
context to resolve.

### Provider symbols

Provider runtime failures currently retain stable IDs through executable
instructions and environment lookups. Diagnostic formatting MAY resolve the
language-facing provider name from the program attribute table.

Execution and environment lookup continue using the provider ID.

## Example 1: user function and local name

This source uses current MuLang syntax:

```text
func divide(total: int, divisor: int): int {
    var result = total / divisor;
    return result;
}

return divide(42, 0);
```

A simplified hypothetical MuIR 2 translation is:

```text
muir 2
mode program
environment "env"
profile "profile"
types 1
type t0 intrinsic int

entry "$entry" t0 bb0 slots 3 blocks 1 lifetime-regions 1 exception-regions 0 {
  lifetime-region lr0 parent none entry bb0
  slot %0 temporary t0 mutable region lr0
  slot %1 temporary t0 mutable region lr0
  slot %2 temporary t0 mutable region lr0
  block bb0 region lr0 instructions 3 {
    ins constant 64 2 %0 t0 int 42
    ins constant 68 1 %1 t0 int 0
    ins user-call 57 13 %2 "user:0" t0 [%0, %1]
    term return 50 21 %2
  }
}
users 1
user "user:0" t0 bb0 slots 3 blocks 1 lifetime-regions 1 exception-regions 0 {
  lifetime-region lr0 parent none entry bb0
  slot %0 parameter t0 readonly region lr0
  slot %1 parameter t0 readonly region lr0
  slot %2 local t0 mutable region lr0
  block bb0 region lr0 instructions 1 {
    ins binary 67 15 %2 divide %0 %1
    term return 89 14 %2
  }
}
attributes 4 {
  attribute function user "user:0" source-name { "divide" }
  attribute slot user "user:0" %0 source-name { "total" }
  attribute slot user "user:0" %1 source-name { "divisor" }
  attribute slot user "user:0" %2 source-name { "result" }
}
end
```

Offsets are illustrative. The executable function ID remains `user:0`.
A division diagnostic or stack trace can display `divide`, `divisor`, and
`result` when the runtime already has the corresponding function or slot
context.

## Example 2: erased type name and provider global alias

This example uses the planned source-defined object-type syntax from
`user-defined-object-types.plan.md`. Assume the host environment also declares
the language-facing global `currentCustomer` with provider ID
`crm.customer.current`.

```text
type Customer {
    id: int,
    name: string
};

func getCustomerName(customer: Customer): string {
    return customer.name;
}

return getCustomerName(currentCustomer);
```

A simplified hypothetical MuIR 2 translation is:

```text
muir 2
mode program
environment "env"
profile "profile"
types 3
type t0 intrinsic int
type t1 intrinsic string
type t2 object closed 2 ["id" t0 required mutable, "name" t1 required mutable]

entry "$entry" t1 bb0 slots 2 blocks 1 lifetime-regions 1 exception-regions 0 {
  lifetime-region lr0 parent none entry bb0
  slot %0 temporary t2 mutable region lr0
  slot %1 temporary t1 mutable region lr0
  block bb0 region lr0 instructions 2 {
    ins load-global 148 15 %0 "crm.customer.current"
    ins user-call 132 32 %1 "user:0" t1 [%0]
    term return 125 40 %1
  }
}
users 1
user "user:0" t1 bb0 slots 2 blocks 1 lifetime-regions 1 exception-regions 0 {
  lifetime-region lr0 parent none entry bb0
  slot %0 parameter t2 readonly region lr0
  slot %1 temporary t1 mutable region lr0
  block bb0 region lr0 instructions 1 {
    ins get-property 101 13 %1 %0 "name" false false
    term return 94 21 %1
  }
}
attributes 4 {
  attribute program provider-global-name { "crm.customer.current" "currentCustomer" }
  attribute function user "user:0" source-name { "getCustomerName" }
  attribute slot user "user:0" %0 source-name { "customer" }
  attribute type t2 source-type-names { 1 ["Customer"] }
}
end
```

If `Customer` and another named type have the same complete structural shape,
the writer emits one structural type entry and one ordinally sorted
`source-type-names` list containing both aliases.

The global load continues using `crm.customer.current`. The source name
`currentCustomer` is available only to diagnostics.

Nested functions are not valid in current MuLang. A future nested-function
feature could add `qualified-source-name` to the existing function target
without changing executable user-function IDs or adding a new target kind.

## Test plan

### Public model

Add tests covering:

- an empty default attribute collection;
- typed program, function, slot, and type lookup;
- immutability of all exposed collections;
- preservation through `with` expressions and program transformations;
- rejection of invalid direct-API targets and duplicate singleton values;
- diagnostic-name fallback when an attribute is absent.

### Compiler lowering

Add compiler tests covering:

- user-function source names associated with compiler-assigned IDs;
- parameter and local names associated with allocated slots;
- no source-name attributes for ordinary temporaries;
- referenced provider global and function names;
- omission of unreferenced provider symbols;
- type-name preservation through `IrTypeProjector`;
- union and ordinal ordering of names for structurally equivalent types;
- exclusion of anonymous synthetic names;
- attribute preservation across expression and program compilation modes.

### Serialization

Add MuIR tests covering:

- canonical version 2 output with an empty attribute section;
- canonical output containing every initial known attribute;
- version 1 and 2 legacy slot-name conversion;
- MuIR 2 slot-name materialization into obsolete `IrSlot.Name`;
- fallback synthesis when only `IrSlot.Name` is supplied through the API;
- rejection of conflicting legacy and attribute slot names;
- deterministic attribute and payload ordering;
- type-target resolution after canonical type-table merging;
- unknown attribute skipping;
- dropping an unknown attribute after read and rewrite;
- rejection of unknown target kinds;
- malformed and unbalanced payloads;
- invalid target references;
- duplicate singleton attributes;
- invalid provider-symbol mappings;
- invalid and duplicate type-name entries;
- every new reader limit;
- LF output and UTF-8 without a byte-order mark.

Retain MuIR 1 and legacy MuIR 2 fixtures as reader-compatibility artifacts.
Add canonical MuIR 2 fixtures for minimal, complete, type-alias, and
unknown-attribute documents.

Update existing tests that currently expect MuIR 2 output to include the
canonical attributes section and attribute-backed slot names.

### Validation

Add validator tests for direct API construction with:

- nonexistent function IDs;
- nonexistent slot IDs;
- types absent from the program;
- duplicate provider IDs;
- invalid source names;
- invalid declaration spans;
- valid empty and partial attribute collections.

### Runtime diagnostics

Add exporter and runtime tests proving:

- stack frames retain portable IDs;
- formatted stack traces prefer source function names;
- missing attributes preserve current diagnostic output;
- provider failures may display language-facing names while dispatch still
  uses stable IDs;
- serialization and deserialization do not change the selected diagnostic
  names.

## Documentation updates

Update:

- `docs/public/ir/portable-intermediate-representation.md`;
- `docs/public/ir/muir-format.md`;
- public API XML documentation and baselines;
- the language-server or runtime documentation where runtime diagnostics and
  stack traces are described;
- the detailed changelog for package release `0.3.0`.

The portable-IR specification should state the non-semantic attribute
invariants. The MuIR specification should contain the complete registry,
payload grammar, canonical ordering, compatibility behavior, and limits.

## Implementation sequence

1. Add the typed public attribute model, empty defaults, targets, lookup
   helpers, and validation rules.
2. Add `IrProgram.Attributes` without changing executable behavior.
3. Add the compiler `IrAttributeBuilder` and populate function, parameter,
   local, and provider-symbol names.
4. Extend `IrTypeProjector` to preserve, merge, and re-target type names after
   structural projection.
5. Update every `IrProgram` transformation to preserve attributes.
6. Add MuIR 2 reader support for the attribute section, known payload parsing,
   unknown-payload skipping, limits, and legacy MuIR 1 and prerelease MuIR 2
   conversion.
7. Update the canonical writer to emit MuIR 2 with the attributes section,
   serialize attributes deterministically, and synthesize compatible slot
   attributes from `IrSlot.Name`.
8. Deprecate `IrSlot.Name`, migrate repository callers to typed attribute
   lookup, and update public API baselines.
9. Add structural attribute validation for directly constructed IR programs.
10. Integrate the shared diagnostic-name resolver into runtime stack traces
    and provider-error formatting without changing dispatch identifiers.
11. Add MuIR fixtures and the complete compiler, serialization, validator, and
    runtime test matrix.
12. Update the normative portable-IR and MuIR specifications, runtime
    documentation, and changelog.

## Completion criteria

The work is complete when:

- the compiler emits the initial well-known names without affecting executable
  behavior;
- MuIR 2 round-trips every understood attribute canonically;
- MuIR 1 and existing prerelease MuIR 2 documents remain readable;
- unknown MuIR 2 attributes on known targets are safely skipped;
- legacy slot names are available through both the obsolete compatibility
  property and the new attribute API;
- structurally merged types retain all meaningful diagnostic aliases without
  changing type identity;
- runtime diagnostics use source names when available and stable fallbacks
  otherwise;
- all reader limits, malformed-input cases, API baselines, specifications, and
  regression tests are updated.
