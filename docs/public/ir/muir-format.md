# MuIR serialization format

MuIR is the canonical textual persistence and interchange format for portable MuLang IR. MuIR files use the `.muir` extension and UTF-8 encoding. The format version is independent from the MuLang language version and language-profile fingerprint.

MuIR reconstructs an `IrProgram`; it does not prove that the program is semantically valid for a host environment. A host MUST apply the following lifecycle:

1. read the MuIR document;
2. reject the document when reading reports an error;
3. select the expected provider environment;
4. validate the reconstructed program through `IrValidator`;
5. export or execute the program only when validation succeeds.

## 1. Lexical structure

A MuIR document consists of Unicode scalar values decoded from UTF-8. Invalid UTF-8 MUST be rejected.

Spaces, horizontal tabs, CRLF line endings, LF line endings, and blank lines are insignificant between tokens. A `#` outside a string begins a comment that continues to the next line ending or end of document.

Keywords and symbolic tokens are case-sensitive. An atom is a non-empty sequence delimited by whitespace, a comment, a string delimiter, or one of:

```text
{ } [ ] , :
```

Strings are delimited by `"`. They support:

| Escape | Value |
|---|---|
| `\"` | quotation mark |
| `\\` | reverse solidus |
| `\n` | line feed |
| `\r` | carriage return |
| `\t` | horizontal tab |
| `\u{H...}` | one Unicode scalar encoded by one through six hexadecimal digits |

Raw line endings, unpaired surrogates, invalid scalar values, and other escapes MUST be rejected.

Non-negative integers use invariant decimal notation without a sign. Signed integer constants may use one leading `-`.

## 2. Document grammar

The following grammar describes MuIR version 2. Repetition counts are declared immediately before the repeated production and MUST match the number of following declarations.

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

compilation-mode ::= "expression" | "program"

entry-function ::= function-header("entry")
user-function  ::= function-header("user")

function-header(kind) ::=
  kind string type-ref block-ref
  "slots" integer
  "blocks" integer
  "lifetime-regions" integer
  "exception-regions" integer
  "{"
    lifetime-region*
    exception-region*
    slot*
    block*
  "}"

 lifetime-region ::=
  "lifetime-region" lifetime-region-ref
  "parent" (lifetime-region-ref | "none")
  "entry" block-ref

exception-region ::=
  "exception-region" exception-region-ref
  "parent" (exception-region-ref | "none")
  "parent-part" ("protected" | "handler" | "cleanup" | "none")
  "protected" block-ref block-list
  "handler" ("none" | block-ref slot-ref block-list)
  "cleanup" ("none" | block-ref block-list)

slot ::=
  "slot" slot-ref slot-kind type-ref slot-mutability
  "region" lifetime-region-ref
slot-kind ::= "parameter" | "local" | "temporary"
slot-mutability ::= "mutable" | "readonly"

block ::=
  "block" block-ref "region" lifetime-region-ref
  "instructions" integer "{"
    instruction*
    terminator
  "}"

instruction ::= "ins" opcode span operands
terminator  ::= "term" terminator-kind span operands
span        ::= integer integer

type-ref  ::= "t" integer
slot-ref  ::= "%" integer
block-ref ::= "bb" integer
lifetime-region-ref ::= "lr" integer
exception-region-ref ::= "er" integer
block-list ::= "[" (block-ref ("," block-ref)*)? "]"

attribute ::=
  "attribute" attribute-target attribute-name
  "{" attribute-payload-token* "}"

attribute-target ::=
    "program"
  | "function" function-target
  | "slot" function-target slot-ref
  | "type" type-ref

function-target ::= "entry" | "user" string
attribute-name ::= atom
```

The two integers in a span are the source start and length. Both MUST fit in a signed 32-bit integer, and their checked sum MUST also fit.

The document MUST contain exactly one entry function. User-function, slot, block, instruction, element, and argument order is preserved. Object properties are normalized by ordinal property name.

Lifetime-region and exception-region identifiers are independently contiguous
and zero-based. Every function serializes lifetime root region `lr0`.
Parameter slots MUST be `readonly`, belong to `lr0`, are implicitly defined at
function entry, and cannot be instruction destinations. Temporary slots MUST
be `mutable`. Read-only local validation is path-sensitive within each
activation of its owning lifetime region.

Exception component block lists have set semantics and canonical output sorts
them by block identifier. A handler declaration includes its mandatory
read-only `error` slot.

## 3. Type table

Type identifiers are contiguous and zero-based. A definition MUST use the next identifier in sequence. Composite definitions may refer to any declared type identifier, including a later definition, so finite object-type graphs can be self-recursive or mutually recursive.

```text
type-definition ::=
    "type" type-ref "intrinsic" intrinsic-type
  | "type" type-ref "nullable" type-ref
  | "type" type-ref "array" array-capability type-ref
  | "type" type-ref "object" openness integer
      "[" object-property-list? "]"

intrinsic-type ::=
    "bool" | "int" | "float" | "number" | "string"
  | "primitive" | "error" | "unknown" | "object" | "void" | "null"

array-capability ::= "mutable" | "readonly"
openness         ::= "open" | "closed"

object-property-list ::= object-property ("," object-property)*
object-property ::=
  string type-ref
  ("required" | "optional")
  ("mutable" | "readonly")
```

The compiler error-recovery type is not representable. A writer MUST fail explicitly when that type occurs.

The reader parses the complete type table before materialization and publishes no partially initialized type. Recursive cycles consisting only of nullable or array wrappers, with no structured-object node, are invalid.

Canonical type identity is the complete wire definition. The writer:

- ignores source `TypeSymbol` reference sharing;
- sorts object properties by name with ordinal comparison;
- computes recursive structural identity coinductively;
- merges bisimilar finite recursive type graphs;
- orders acyclic dependencies before dependants;
- permits forward references within a recursive strongly connected component.

Openness, property name, optionality, mutability, property type, and array
capability remain identity-bearing. Property declaration order does not.

Version 1 object properties omit the mutability token and are read as mutable.
Version 2 writers always emit both independent capability tokens.

Provider type IDs, language-facing object type names, and named-versus-anonymous origin are not represented. The environment fingerprint identifies the required provider schema, while IR validation compares the reconstructed object type structurally.

Source-declared and provider type names do not participate in the type table.
Reachable names may be represented by `source-type-names` diagnostic
attributes. Unused source declarations remain absent, and changing only a
diagnostic alias does not change canonical executable type identity.

## 4. Well-known attributes

MuIR 2 defines a closed catalog of optional diagnostic attributes:

| Attribute | Target | Payload | Cardinality |
|---|---|---|---|
| `source-document` | program | one absolute URI string | zero or one |
| `source-line-map` | program | source length, line count, and line-start offsets | zero or one |
| `provider-global-name` | program | provider ID and language name | zero or more, unique by provider ID |
| `provider-function-name` | program | provider ID and language name | zero or more, unique by provider ID |
| `source-name` | function or slot | one string | zero or one per target |
| `qualified-source-name` | function | one string | zero or one |
| `declaration-span` | function or slot | one span | zero or one per target |
| `source-type-names` | type | a non-empty string list | zero or one |

Known payloads are:

```text
source-document-payload      ::= string
source-line-map-payload      ::= integer integer integer*
provider-symbol-name-payload ::= string string
source-name-payload          ::= string
qualified-name-payload       ::= string
declaration-span-payload     ::= span
source-type-names-payload    ::= integer "[" (string ("," string)*)? "]"
```

Source document URIs MUST be absolute and are serialized using their original
string representation. Names and provider IDs MUST NOT be
empty or whitespace. Source type names MUST
be unique and ordinally sorted, and their declared count MUST match the list.
The first integer in a source line map is the source length in Unicode scalar
values. The second integer is the line count and MUST match the number of
following line-start offsets. A line map MUST contain at least one line start;
the first MUST be zero, and subsequent offsets MUST be strictly increasing and
no greater than the source length. Instruction, terminator, and declaration
spans MUST remain within the mapped source length.
Unknown attribute names on known targets are skipped and discarded through
their balanced-brace payload. Unknown target kinds, malformed targets,
unbalanced payloads, invalid known payloads, duplicate singleton attributes,
and conflicting slot names are errors.

Attributes do not affect executable semantics. Unknown attributes accepted by
an older reader are not preserved when that program is rewritten.

## 5. Constants

Constant operands encode runtime representation explicitly:

```text
constant ::=
    "null"
  | "bool" ("true" | "false")
  | "int" signed-integer
  | "float" float-value
  | "string" string

float-value ::=
    finite-binary64
  | "positive-infinity"
  | "negative-infinity"
  | "nan"
  | "negative-zero"
```

Finite binary64 values use invariant round-trip notation. `negative-zero` preserves the negative-zero bit pattern. `nan` represents the MuLang NaN value; payload and sign bits are not preserved.

Arbitrary host objects are not portable constants and MUST be rejected by the writer.

A constant whose static type is `primitive` uses the token for its concrete
runtime representation. No separate primitive constant token exists.

## 6. Instructions

Every instruction starts with its opcode and source span. Operand order is fixed:

| Opcode | Operands after span |
|---|---|
| `constant` | destination, type, constant |
| `copy` | destination, source |
| `load-global` | destination, global ID string |
| `unary` | destination, unary operator, operand |
| `binary` | destination, binary operator, left, right |
| `convert` | destination, source, target type, conversion kind |
| `truthiness` | destination, source |
| `type-test` | destination, source, tested type |
| `is-null` | destination, source |
| `has-property` | destination, target, key |
| `create-array` | destination, array type, slot list |
| `create-object` | destination, object type, property-value list |
| `get-property` | destination, target, name string, optional flag, array-length flag |
| `set-property` | target, name string, value |
| `remove-property` | target, name string |
| `get-element` | destination, target, index, object-access flag, optional flag |
| `set-element` | target, index, value, object-access flag |
| `remove-element-property` | target, key |
| `provider-call` | destination or `none`, function ID string, return type, argument list |
| `user-call` | destination or `none`, function ID string, return type, argument list |

A slot list is `[` followed by comma-separated slot references and `]`.

A property-value list is `[` followed by comma-separated pairs of a string, `:`, and a slot reference, then `]`.

Boolean flags use `true` and `false`.

Unary operator tokens are:

| Token | IR operator |
|---|---|
| `identity` | identity |
| `negate` | numeric negation |
| `logical-not` | logical negation |
| `bitwise-not` | bitwise complement |

Binary operator tokens are:

```text
add subtract multiply divide remainder
left-shift right-shift
less-than less-than-or-equal greater-than greater-than-or-equal
structural-equal structural-not-equal
identity-equal identity-not-equal
bitwise-and bitwise-xor bitwise-or
```

Conversion-kind tokens are `value` and `checked`.

Unknown opcodes and operator tokens MUST be rejected. A reader MUST NOT ignore an executable operation it does not understand.

## 7. Terminators

Terminator operands after the source span are:

| Token | Operands |
|---|---|
| `jump` | target block |
| `branch` | condition slot, true block, false block |
| `return` | value slot or `none` |
| `throw` | error-value slot |
| `resume` | no operands |

Every block contains exactly one terminator after its declared instructions.

`resume` is valid only in a cleanup component. A cleanup cannot return.
Ordinary control-flow edges cannot enter handlers or cleanup components.

## 8. Canonical form

The canonical writer MUST:

- emit UTF-8 without a byte-order mark when writing a stream;
- use LF line endings;
- use two spaces per indentation level;
- emit one space between ordinary tokens;
- emit no comments or trailing whitespace;
- emit one final line feed;
- preserve function, slot, block, instruction, element, and argument order;
- emit the MuIR 2 `attributes` section even when empty;
- order targets as program, function, slot, then type;
- order functions with entry first and users in document order;
- order slots by function and slot ID and types by type-table ID;
- order attributes for one target by attribute name;
- order provider mappings by provider ID and type aliases ordinally;
- emit object properties by ordinal name;
- assign type identifiers deterministically, with acyclic dependencies before dependants and stable ordering inside recursive components;
- use the tokens and operand orders defined by this section.

Serializing wire-equivalent IR graphs MUST produce byte-identical output regardless of equivalent type-instance sharing or object-property declaration order. Reading canonical MuIR and writing the reconstructed program MUST reproduce the same bytes.

The default writer options emit the complete canonical document. Writers MAY
offer an explicit diagnostic-metadata projection that omits the
`source-line-map` attribute or all diagnostic attributes. Such output remains
canonical for the projected metadata set but is intentionally lossy: reading
and rewriting it cannot restore omitted attributes. The MuIR 2 `attributes`
section remains present even when all attributes are omitted.

`MuIrWriterOptions.WriteAttributes` controls whether any diagnostic attributes
are emitted. `MuIrWriterOptions.WriteSourceLineMap` independently controls the
`source-line-map` attribute when attribute writing is enabled.

Readers MAY accept non-canonical whitespace and comments, but writers MUST emit only canonical form.

## 9. Reading, diagnostics, and limits

MuIR reading distinguishes structural reconstruction from semantic IR validation. A successful read guarantees that the document is lexically and structurally representable by the public IR model. It does not guarantee valid control flow, definite assignment, provider identifiers, type compatibility, or environment/profile fingerprints.

Reading failure returns no partial program and at least one error diagnostic. A diagnostic contains:

- a stable `MUIR` code;
- severity;
- zero-based document offset and length;
- one-based line and column;
- a message.

The reader MUST reject unsupported format versions, invalid references, invalid type definitions, malformed instructions and terminators, source-span overflow, trailing content, and constructor failures.

Hosts MAY configure positive limits for document, string, token, type-nesting,
type, function, slot, block, instruction, list-element, diagnostic, total
attribute, per-target attribute, skipped unknown-payload token, source-type
name, and provider-symbol mapping counts. A limit violation MUST fail reading
without returning a partial program.

## 10. Compatibility

MuIR version 2 adds the `primitive` intrinsic-type token, structured-object
property mutability, lifetime and exception regions, and well-known
attributes. The reader continues to accept version 1 documents, reads their
properties as mutable, and converts positional slot names into `source-name`
attributes while preserving the obsolete compatibility property.

Earlier prerelease MuIR 2 layouts are not compatibility formats and MUST be
rejected. A MuIR 2 document therefore always contains the `attributes` section
and has no positional slot-name operand. The canonical writer emits version 2.

MuIR compatibility is defined by the wire contract, not by .NET enum names,
record names, or numeric enum values.

Changing an existing token, operand order, required field, or semantic interpretation requires a new MuIR format version. Adding an instruction, terminator, type form, or other required executable construct also requires a new version.

A reader MUST reject unknown required constructs and unsupported versions
rather than guessing. Version 1 fixtures remain reader-compatibility artifacts;
rewriting version 1 input with the current writer upgrades it to version 2.
