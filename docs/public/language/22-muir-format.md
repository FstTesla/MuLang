# 22. MuIR serialization format

MuIR is the canonical textual persistence and interchange format for portable MuLang IR. MuIR files use the `.muir` extension and UTF-8 encoding. The format version is independent from the MuLang language version and language-profile fingerprint.

MuIR reconstructs an `IrProgram`; it does not prove that the program is semantically valid for a host environment. A host MUST apply the following lifecycle:

1. read the MuIR document;
2. reject the document when reading reports an error;
3. select the expected provider environment;
4. validate the reconstructed program through `IrValidator`;
5. export or execute the program only when validation succeeds.

## 22.1. Lexical structure

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

## 22.2. Document grammar

The following grammar describes MuIR version 1. Repetition counts are declared immediately before the repeated production and MUST match the number of following declarations.

```text
document ::=
  "muir" "1"
  "mode" compilation-mode
  "environment" string
  "profile" string
  "types" integer type-definition*
  entry-function
  "users" integer user-function*
  "end"

compilation-mode ::= "expression" | "program"

entry-function ::= function-header("entry")
user-function  ::= function-header("user")

function-header(kind) ::=
  kind string type-ref block-ref
  "slots" integer
  "blocks" integer
  "{"
    slot*
    block*
  "}"

slot ::= "slot" slot-ref slot-kind type-ref slot-mutability (string | "none")
slot-kind ::= "parameter" | "local" | "temporary"
slot-mutability ::= "mutable" | "readonly"

block ::=
  "block" block-ref "instructions" integer "{"
    instruction*
    terminator
  "}"

instruction ::= "ins" opcode span operands
terminator  ::= "term" terminator-kind span operands
span        ::= integer integer

type-ref  ::= "t" integer
slot-ref  ::= "%" integer
block-ref ::= "bb" integer
```

The two integers in a span are the source start and length. Both MUST fit in a signed 32-bit integer, and their checked sum MUST also fit.

The document MUST contain exactly one entry function. User-function, slot, block, instruction, element, and argument order is preserved. Object properties are normalized by ordinal property name.

Parameter slots MUST be `readonly`, are implicitly defined at function entry, and cannot be instruction destinations. Temporary slots MUST be `mutable`. A read-only local has exactly one syntactic defining instruction; `IrValidator` enforces that constraint together with ordinary definite assignment.

## 22.3. Type table

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
  | "unknown" | "object" | "void" | "null"

array-capability ::= "mutable" | "readonly"
openness         ::= "open" | "closed"

object-property-list ::= object-property ("," object-property)*
object-property ::= string type-ref ("required" | "optional")
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

Openness, property name, optionality, property type, and array capability remain identity-bearing. Property declaration order does not.

Provider type IDs, language-facing object type names, and named-versus-anonymous origin are not represented. The environment fingerprint identifies the required provider schema, while IR validation compares the reconstructed object type structurally.

## 22.4. Constants

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

## 22.5. Instructions

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

## 22.6. Terminators

Terminator operands after the source span are:

| Token | Operands |
|---|---|
| `jump` | target block |
| `branch` | condition slot, true block, false block |
| `return` | value slot or `none` |

Every block contains exactly one terminator after its declared instructions.

## 22.7. Canonical form

The canonical writer MUST:

- emit UTF-8 without a byte-order mark when writing a stream;
- use LF line endings;
- use two spaces per indentation level;
- emit one space between ordinary tokens;
- emit no comments or trailing whitespace;
- emit one final line feed;
- preserve function, slot, block, instruction, element, and argument order;
- emit object properties by ordinal name;
- assign type identifiers deterministically, with acyclic dependencies before dependants and stable ordering inside recursive components;
- use the tokens and operand orders defined by this section.

Serializing wire-equivalent IR graphs MUST produce byte-identical output regardless of equivalent type-instance sharing or object-property declaration order. Reading canonical MuIR and writing the reconstructed program MUST reproduce the same bytes.

Readers MAY accept non-canonical whitespace and comments, but writers MUST emit only canonical form.

## 22.8. Reading, diagnostics, and limits

MuIR reading distinguishes structural reconstruction from semantic IR validation. A successful read guarantees that the document is lexically and structurally representable by the public IR model. It does not guarantee valid control flow, definite assignment, provider identifiers, type compatibility, or environment/profile fingerprints.

Reading failure returns no partial program and at least one error diagnostic. A diagnostic contains:

- a stable `MUIR` code;
- severity;
- zero-based document offset and length;
- one-based line and column;
- a message.

The reader MUST reject unsupported format versions, invalid references, invalid type definitions, malformed instructions and terminators, source-span overflow, trailing content, and constructor failures.

Hosts MAY configure positive limits for document, string, token, type-nesting, type, function, slot, block, instruction, list-element, and diagnostic counts. A limit violation MUST fail reading without returning a partial program.

## 22.9. Compatibility

MuIR version 1 compatibility is defined by this wire contract, not by .NET enum names, record names, or numeric enum values.

Changing an existing token, operand order, required field, or semantic interpretation requires a new MuIR format version. Adding an instruction, terminator, type form, or other required executable construct also requires a new version.

A version 1 reader MUST reject unknown required constructs and unsupported versions rather than guessing. Canonical fixtures are compatibility artifacts and MUST remain byte-stable.
