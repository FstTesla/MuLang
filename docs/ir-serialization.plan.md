# MuIR Canonical Type Graph and Slot Capability Plan

## Goal

Strengthen MuIR canonicalization by deduplicating type-table entries according
to their complete MuIR wire definition rather than `TypeSymbol` reference
identity, while supporting recursive structured-object graphs and persisted
read-only local slots.

After this change, two IR graphs that differ only in whether equivalent
composite `TypeSymbol` instances are shared MUST serialize to byte-identical
MuIR.

The change must:

- preserve every piece of type metadata represented by MuIR;
- support self-recursive and mutually recursive object types;
- materialize recursive type graphs atomically;
- validate read-only local definitions in `IrValidator`;
- preserve deterministic first-occurrence ordering;
- retain canonical serialize-read-serialize behavior;
- preserve existing constructors and defaults for non-recursive types and
  mutable slots;
- canonicalize structured-object properties by ordinal name;
- keep recursive equivalence and canonicalization bounded and deterministic.

## Current behavior

`MuIrWriter.CollectTypes` currently maintains:

- a `Dictionary<TypeSymbol, int>` using `ReferenceEqualityComparer`;
- an active reference-identity set for recursive-definition detection;
- one type-table entry per distinct in-memory `TypeSymbol` instance.

`ObjectTypeSymbol` currently requires a complete property collection in its
constructor, so public callers cannot construct self-recursive or mutually
recursive immutable object graphs.

`IrSlot` currently transports ID, kind, type, and source name, but no
mutability capability. `IrValidator` therefore cannot distinguish a
read-only local initializer from later writes.

Intrinsic types are already naturally deduplicated because `TypeSymbols`
exposes singleton instances. Composite factories are not interned:

- `TypeSymbols.Nullable` returns a new `NullableTypeSymbol`;
- `TypeSymbols.Array` returns a new mutable `ArrayTypeSymbol`;
- `TypeSymbols.ReadOnlyArray` returns a new read-only `ArrayTypeSymbol`;
- `ObjectTypeSymbol.CreateAnonymous` returns a new anonymous object type.

Consequently, semantically and wire-identical composite instances can produce
duplicate type-table entries. The output is canonical for one object graph,
but not necessarily for two graphs with the same complete MuIR content and
different instance sharing.

## Non-goals

This work does not:

- add global or process-wide type interning;
- change `TypeSymbol` equality or hashing;
- change `TypeRelations.AreEquivalent`;
- merge types based only on language-level assignability or equivalence;
- reorder functions, slots, blocks, instructions, or operands;
- change provider type registration or environment fingerprint identity;
- add source syntax for user-defined object declarations or read-only locals;
- add read-only object properties;
- add function types or first-class function values;
- expose partially initialized `TypeSymbol` instances.

## Wire identity contract

Two types are deduplicated only when every field emitted by the canonical MuIR
writer is identical.

All string comparisons use `StringComparer.Ordinal`.

### Intrinsic types

Intrinsic wire identity consists of:

- the `intrinsic` type form;
- the stable MuIR intrinsic token.

The token distinguishes:

- `bool`;
- `int`;
- `float`;
- `number`;
- `string`;
- `unknown`;
- `object`;
- `void`;
- `null`.

The compiler error-recovery type remains unrepresentable.

### Nullable types

Nullable wire identity consists of:

- the `nullable` type form;
- the canonical identity of the underlying type.

Two separately allocated nullable types therefore share an entry when their
underlying types have the same complete wire definition.

### Array types

Array wire identity consists of:

- the `array` type form;
- mutable or read-only capability;
- the canonical identity of the element type.

Mutable and read-only arrays never share an entry.

### Structured object types

Structured-object wire identity consists of:

- the `object` type form;
- open or closed state;
- property count;
- the property set sorted by ordinal property name.

Each property contributes:

- property name;
- canonical identity of its type;
- required or optional state.

Property order is not part of wire identity. Before building the key, the
writer sorts properties by name with `StringComparer.Ordinal`. Object types
with the same complete property definitions in different declared orders
therefore share one entry and produce the same canonical bytes.

Provider type ID, language-facing name, and named-versus-anonymous origin are
erased by lowering and excluded from MuIR. Provider and user types with the
same complete structural definition share an entry.

## Fields intentionally excluded

The wire key MUST NOT use:

- object reference identity;
- CLR type names;
- process-specific hash codes;
- `TypeSymbol.DisplayName` as a substitute for structured metadata;
- `TypeRelations.AreEquivalent`;
- assignability or castability;
- provider type ID;
- language-facing object type name;
- named-versus-anonymous origin;
- the source occurrence that first referenced the type;
- final textual escaping or formatting.

`TypeRelations.AreEquivalent` is deliberately excluded because it represents
language semantics, not complete persistence identity. In particular, it can
consider structured types equivalent without preserving every named-type
metadata field transported by MuIR.

## Internal model

Represent type collection as a finite directed graph rather than a tree.

Each discovered source node contains:

- one discriminant for each MuIR type form;
- the intrinsic token where applicable;
- child edges for nullable and array forms;
- array capability;
- structured-object openness;
- an immutable property-key sequence sorted by ordinal property name.

Each property key should contain:

- ordinal property name;
- a child graph edge;
- optionality.

The graph model must be independent from final type IDs. It must support
edges to nodes that have not yet been assigned an emitted ID.

Canonical nodes represent equivalence classes in the graph's stable
coinductive partition. A canonical node contains:

- its scalar wire metadata;
- labelled edges to canonical nodes;
- the sorted canonical property sequence;
- one representative `TypeSymbol` for non-property emission metadata;
- its strongly connected component;
- its final type-table ID.

## Collection algorithm

Collection proceeds in four stages.

### Graph discovery

Traverse IR type roots in the existing deterministic order. Cache discovered
nodes by source reference identity so every source object becomes one graph
node.

When visiting a structured object:

1. create and cache its graph node before following properties;
2. sort properties by ordinal name;
3. add labelled edges to each property type;
4. revisit an existing graph node when an edge closes a cycle.

Reference identity is used only to discover the finite source graph. A
back-edge is valid and no longer produces a serialization exception.

Reject `TypeKind.Error` during discovery.

### Coinductive partitioning

Compute complete wire equivalence through deterministic partition refinement.

1. Assign initial colors from scalar wire metadata and ordered edge labels,
   excluding edge targets.
2. Build each refinement signature from scalar metadata, edge labels, and the
   previous color of every edge target.
3. Sort distinct signatures ordinally and assign deterministic new colors.
4. Repeat until the partition is stable.

Nodes in the same final partition are wire-equivalent and become one
canonical node. This intentionally applies equi-recursive structural
equivalence: bisimilar finite recursive graphs share one wire definition even
when their source cycle lengths or instance sharing differ.

Signature equality and hashing must be explicit and ordinal. Do not use
`TypeRelations.AreEquivalent`, final type IDs, process hashes, or source
encounter ordinals as semantic inputs.

### Strongly connected components

Build the quotient graph of canonical nodes and compute its strongly
connected components.

Order the SCC condensation graph with dependencies before dependants. Within
one recursive SCC, order canonical nodes by their stable final refinement
signature. Forward references are allowed among nodes in the same SCC.

This produces deterministic IDs independent from:

- source object identity;
- duplicate equivalent nodes;
- property declaration order;
- DFS back-edge shape;
- source construction order inside a recursive group.

### Source-reference mapping

Map every encountered source `TypeSymbol` instance to its canonical partition
and final type ID. `TypeReference` continues accepting the original type
instance stored by an IR node and resolves it through this map.

The existing IR traversal order remains unchanged:

1. entry-function return type;
2. entry-function slots;
3. entry-function instruction-owned types;
4. user functions in declared order, using the same per-function order;
5. nested type edges in canonical wire order.

For unrelated acyclic components with no dependency ordering constraint,
first unique IR occurrence remains the final tie-breaker. Recursive SCC
members never use source encounter order as a semantic tie-breaker.

## Writer integration

Change the writer's type state to expose:

- an ordered quotient graph of canonical type nodes;
- a reference map from every encountered `TypeSymbol` instance to the
  canonical node or type ID.

`TypeReference` continues to accept the original `TypeSymbol` stored by an IR
node. It resolves that instance through the reference cache and emits the
canonical node's ID.

`WriteType` emits scalar metadata from the representative and structured
properties from the canonical node in ordinal name order. Because the wire
key includes every emitted field, any source instance represented by that
node produces the same definition.

Type definitions may reference any valid `tN`, including a later definition
in the same recursive SCC. Keep unsupported-type failures explicit through
`MuIrSerializationException`.

## Reader behavior

The type-reference token grammar remains unchanged, but the backward-only
reference restriction is removed.

The reader must parse every type definition into an unresolved descriptor
before constructing any `TypeSymbol`. After all definitions are available,
it validates references, builds the type graph atomically, and only then
constructs functions and the final `IrProgram`.

Undefined references, duplicate type IDs, incomplete definitions, and type
graphs that cannot be finalized fail without exposing a partial graph.

The reader may continue accepting a non-canonical document that declares the
same complete wire definition more than once. Re-serializing that program
will collapse duplicate definitions into canonical form.

The reader may also accept object properties in any declaration order.
Re-serialization sorts those properties by ordinal name.

The normative specification should distinguish:

- accepted MuIR, which may contain redundant type definitions;
- canonical MuIR, which contains one entry per complete wire definition.

Canonical fixtures MUST remain byte-identical after read and write.

Environment compatibility and provider identity remain the responsibility of
the program environment fingerprint, `IrValidator`, and the host environment.

## Atomic type-graph builder

Add a Core builder for immutable recursive object graphs.

The public builder API should:

- declare named and anonymous object handles without publishing a
  `TypeSymbol`;
- express property types through builder-owned type expressions that can
  reference declared handles and existing intrinsic types;
- compose nullable, mutable-array, and read-only-array expressions around
  handles;
- define every object's metadata and property set exactly once;
- validate duplicate builder keys and properties, invalid composites, and
  incomplete handles;
- finalize the complete graph in one operation;
- return immutable completed `ObjectTypeSymbol` instances or an immutable
  handle-to-symbol result.

Internally, finalization may allocate object shells and connect them in two
phases, but shells MUST remain inaccessible until every definition validates
and the graph is complete. Published `ObjectTypeSymbol` instances remain
immutable and thread-safe.

Keep existing `ObjectTypeSymbol` constructors and
`CreateAnonymous` behavior unchanged for ordinary acyclic callers.

The compiler and MuIR reader should use the same builder rather than
maintaining separate recursive-type construction mechanisms. Environment
hosts use the builder result with the existing `EnvironmentBuilder.AddType`
flow, where duplicate provider IDs and language names remain invalid.

## Recursive semantic operations

Update operations that recursively compare or traverse type definitions.

At minimum:

- `TypeRelations.AreEquivalent` must track active type pairs and terminate
  coinductively;
- assignability, castability, common-type, and view-compatibility paths that
  recurse through `AreEquivalent` must preserve their existing semantics;
- `IrValidator` must compare recursive deserialized types against recursive
  environment types without stack overflow;
- environment referenced-type validation must retain same-instance
  registration requirements for provider named types while accepting cycles;
- fingerprint generation must terminate and remain deterministic for
  recursive environment graphs;
- exporter/runtime type traversal must be audited for recursive static-type
  graphs independently from its existing cyclic runtime-value protection.

Tests must distinguish logical type-pair recursion from runtime value-graph
recursion.

## Read-only local slots

Add an explicit `IrSlotMutability` enum:

- `Mutable = 0`;
- `ReadOnly = 1`.

Preserve the current four-argument `IrSlot` constructor and deconstruction
shape. Existing construction maps to `Mutable`. Add an API for constructing a
slot with explicit capability without changing existing call sites.

Parameter slots are read-only and implicitly defined at function entry.
`IrValidator` rejects mutable parameters and every instruction that defines a
parameter slot. Temporary slots remain mutable.

For a read-only local, `IrValidator` must require:

- exactly one syntactic defining instruction in the function;
- no other instruction that writes that slot;
- ordinary definite-assignment rules before every read.

The defining instruction may execute repeatedly when its source declaration
is inside a loop; read-only means one permitted definition site, not one
runtime write for the entire function invocation.

Compiler lowering for a future read-only local declaration must converge all
initializer control flow before that one definition site. Assignments after
initialization remain binding errors and would also be rejected if present in
custom IR.

MuIR slot declarations add a mandatory `mutable` or `readonly` token. Current
local and temporary slots serialize as `mutable`; parameter slots serialize
as `readonly`. Slot capability does not contribute to the type-table wire
identity.

The canonical slot grammar becomes:

```text
slot ::= "slot" slot-ref slot-kind type-ref slot-mutability (string | "none")
slot-mutability ::= "mutable" | "readonly"
```

## Complexity and resource behavior

Graph discovery and SCC construction should be linear in:

- encountered type references;
- distinct source type nodes;
- structured-object properties.

Partition refinement may require multiple passes over nodes and edges. It
must terminate because every pass either stabilizes or strictly splits a
finite partition. Avoid recursive hashing of descendant graphs and avoid
unbounded recursion on the CLR stack.

Document and test practical bounds through existing MuIR reader limits.

## Test plan

### Sharing-independent canonical output

Build pairs of `IrProgram` graphs with the same complete wire definitions but
different instance sharing:

- one shared nullable instance versus multiple fresh nullable instances;
- one shared mutable-array instance versus multiple fresh mutable arrays;
- one shared read-only array instance versus multiple fresh read-only arrays;
- shared nested nullable/array combinations versus freshly rebuilt trees;
- one shared anonymous object type versus separately created identical
  anonymous types;
- provider object types with different IDs and names but the same structural
  metadata;
- object types with the same properties declared in different orders.

For each pair, require:

- byte-identical MuIR;
- the same type count;
- the same type-reference IDs at every occurrence;
- successful read-write canonical round trip.

### Wire-distinct metadata

Require separate type entries when any complete wire field differs:

- intrinsic token;
- nullable underlying type;
- array capability;
- array element type;
- open versus closed state;
- property count;
- property name;
- property optionality;
- property type.

Include cases that differ in every persisted structural field and require
distinct entries.

### Recursive graph canonicalization

Verify that:

- acyclic child definitions precede parents;
- self-recursive provider and anonymous structural objects round-trip as
  structural MuIR objects;
- mutually recursive environment object types round-trip;
- mutually recursive structural user types do not require persisted source
  names;
- recursive edges through nullable, mutable-array, and read-only-array forms
  round-trip;
- bisimilar recursive graphs with different instance sharing or cycle
  expansion produce byte-identical MuIR;
- non-bisimilar recursive graphs remain distinct;
- forward references inside an SCC materialize atomically;
- malformed or undefined recursive references return no partial graph;
- property child IDs are assigned in ordinal property-name order;
- first unique occurrence remains deterministic across functions, slots, and
  instruction-owned types.

### Read-only slot validation

Verify that:

- existing local and temporary slots serialize as `mutable`;
- existing parameter slots default to and serialize as `readonly`;
- explicit mutable and read-only local slots round-trip;
- mutable parameters, parameter definition sites, and read-only temporaries
  are rejected;
- a read-only local with one definition site is accepted;
- zero or multiple definition sites are rejected;
- reads before definite assignment remain rejected;
- a definition site in a loop is accepted while another write site is not;
- mutable slot behavior is unchanged.

### Existing format coverage

Retain and run:

- minimal and complete canonical fixtures;
- all instruction and operator token tests;
- special and finite floating-point constant tests;
- malformed-input and resource-limit tests;
- compiler to MuIR to reader to validator to exporter execution tests.

If fixture bytes change unexpectedly, treat that as an implementation defect.
This implementation intentionally updates the fixtures once to:

- sort structured properties by ordinal name;
- include the mandatory slot mutability token.

After that reviewed migration, fixture bytes remain compatibility-locked.

### Performance regression coverage

Add a focused test with many freshly allocated but wire-identical composite
types. Verify that:

- the output contains one canonical definition;
- serialization completes without recursive growth proportional to the
  number of repeated occurrences;
- no fixed wall-clock threshold is required.

Add recursive stress cases with large SCCs and deep acyclic tails. Verify
termination through configured structural limits without fixed wall-clock
assertions.

## Documentation updates

Update `docs/public/language/22-muir-format.md` to specify:

- complete wire-definition identity;
- canonical deduplication independent of `TypeSymbol` sharing;
- property-order independence and ordinal property emission;
- forward type references and recursive SCC ordering;
- coinductive equivalence of recursive wire graphs;
- slot mutability tokens and validation scope;
- acceptance and normalization of redundant non-canonical definitions;
- omission of provider ID, object type name, and named-versus-anonymous
  origin.

No changelog entry is required if this change lands before the first release
that contains MuIR. If MuIR has already been released, record the stronger
canonicalization guarantee and assess whether the format version must change.

## Future language extensions

The canonical wire-definition model should remain extensible without turning
future source declarations into nominal MuIR types by default.

### User-defined object types

User-defined object types are expected to behave structurally, like declared
object shapes rather than general aliases or nominal classes.

The compiler should resolve a user-defined object declaration to the ordinary
structured `ObjectTypeSymbol` shape before lowering. Its source declaration
name should not be persisted in MuIR and should not contribute to wire
identity.

Two user-defined object declarations with the same complete structural
definition should therefore share one canonical MuIR type even when their
source names differ.

Provider object types retain stable IDs and language-facing names in the
environment model and environment fingerprint. Lowering projects them to
anonymous structural IR types, so those fields do not enter MuIR.

No separate nominal type-declaration table is needed solely for structural
user-defined object types.

Recursive user-defined object shapes are supported through the shared atomic
type-graph builder. Their source declaration names are still erased before
lowering. MuIR persists the recursive structural graph through ordinary
`tN` references, including forward references within a strongly connected
component.

### Configurable open-property type

If an open object can specify the type of undeclared properties rather than
always using `unknown?`, the resolved residual property type becomes part of:

- `ObjectTypeSymbol` or its future equivalent;
- the MuIR object definition;
- complete wire identity;
- runtime conformance and IR validation.

The residual type should be canonicalized like any other child type and
emitted before the containing object. Existing documents would imply the
historical `unknown?` residual type. Adding the field likely requires a new
MuIR format version unless the first released grammar reserves an
unambiguous backward-compatible representation.

### Read-only object properties

If properties gain a read-only modifier, each property key must additionally
contain its mutability capability. Required/optional and mutable/read-only
remain independent dimensions.

Property sorting continues to use ordinal property name. Two otherwise
identical object types that differ in one property's write capability remain
wire-distinct.

The validator must reject property writes through a read-only property. A
legacy property definition would imply the historical mutable capability.
Persisting this semantic field likely requires a new MuIR format version.

### Read-only local variables

Local-variable mutability is persisted and validated in `IrValidator`.
`IrSlot` carries the capability and MuIR emits it explicitly. Existing locals
and temporaries default to mutable; existing parameters default to read-only,
matching their established source semantics.

### First-class functions and function types

Function types should be structural. Their complete wire identity should
contain every call-signature field with runtime or validation significance,
including:

- parameter types in declared order;
- return type;
- variadic state, if supported;
- calling capability, effects, or other invocation modifiers, if supported.

Source aliases or declaration names for function types should not be
persisted. Equivalent signatures should share one canonical function-type
entry.

Function values remain distinct from function types. Two functions with the
same signature are different values and bodies. Top-level function values can
refer to stable `IrFunction` IDs already carried by the program. New
instructions will be required to load, pass, store, and invoke function
values.

If closures are introduced, MuIR must also represent captured values and the
relationship between a function body and its capture environment. Function
parameter order remains identity-bearing even though object-property order
does not.

Adding a function type form, first-class function instructions, or closure
representation requires a new MuIR format version.

### Compatibility rule for future fields

Every future field with execution or validation semantics must be included in
the complete wire-definition key. Metadata erased before lowering must not be
persisted merely because it existed in source.

Version 1 documents retain their historical defaults. A version 1 reader must
reject documents using future type forms, fields, or instructions rather than
guessing their meaning.

## Implementation sequence

### Phase 1: Freeze identity rules

- add tests that demonstrate the current instance-sharing-dependent output;
- encode all complete wire fields in test cases;
- confirm differing property order produces identical canonical bytes;
- define coinductive equality for recursive structural graphs;
- define deterministic SCC and forward-reference ordering;
- define read-only local definition-site validation;
- confirm `TypeRelations.AreEquivalent` is not used.

### Phase 2: Add atomic recursive type construction

- add the Core type-graph builder and opaque handles;
- preserve existing acyclic construction APIs;
- use internal shells only during atomic finalization;
- update environment validation and fingerprints for cycles;
- make `TypeRelations` recursive comparisons coinductive;
- add self-recursive and mutually recursive Core tests.

### Phase 3: Introduce canonical wire graph nodes

- add internal graph-node and sorted property-edge models;
- implement deterministic partition refinement;
- merge bisimilar nodes into canonical quotient nodes;
- compute SCCs and stable IDs with forward references;
- resolve every original type occurrence through the reference cache;
- emit one representative per canonical graph partition;
- retain existing serialization errors.

### Phase 4: Integrate the reader

- parse all type declarations into unresolved descriptors;
- resolve forward and backward references;
- materialize through the shared atomic builder;
- expose no symbols before successful graph finalization;
- add malformed recursive graph and reader-limit tests.

### Phase 5: Add read-only slot capability

- add slot mutability while preserving the existing constructor and
  deconstruction shape;
- add mandatory MuIR slot capability tokens;
- identify every instruction destination consistently;
- enforce one definition site for read-only locals in `IrValidator`;
- enforce implicit definition and no write sites for read-only parameters;
- retain existing mutable-local and temporary behavior;
- add writer, reader, validator, and control-flow tests.

### Phase 6: Expand compatibility tests

- add sharing-independent equality tests;
- add every metadata-distinction test;
- add nested, recursive, SCC, and high-duplication tests;
- rerun canonical fixture and end-to-end tests.

### Phase 7: Update specification and validate

- update the normative MuIR canonicalization rules;
- update IR and type-system documentation for recursive graphs and slot
  capability;
- run formatter and analyzers;
- run targeted MuIR and exporter tests;
- run the complete solution in Debug and Release;
- build DocFX with warnings as errors;
- inspect the final diff for unrelated changes.

## Acceptance criteria

The work is complete when:

- no type-table lookup uses `TypeRelations.AreEquivalent` or source reference
  identity as the deduplication rule;
- reference identity is used only for finite source-graph discovery and
  source-to-canonical mapping;
- complete wire-equivalent types share exactly one type-table entry;
- bisimilar recursive wire graphs share canonical partitions;
- any difference in emitted type metadata produces a distinct entry;
- property declaration order is normalized by ordinal property name and does
  not produce a distinct entry;
- acyclic type IDs remain dependency-first;
- recursive SCCs use deterministic IDs and valid forward references;
- equivalent IR graphs with different type-instance sharing serialize to
  byte-identical MuIR;
- recursive environment and user structural object graphs can be built
  atomically and remain immutable after publication;
- `TypeRelations`, environment validation, fingerprints, `IrValidator`, and
  exporters terminate on supported recursive static-type graphs;
- existing `IrSlot` construction remains mutable by default;
- read-only locals round-trip and `IrValidator` enforces one definition site;
- canonical fixtures are intentionally migrated once for property sorting and
  slot capability, then remain byte-identical;
- read-write canonical round trips remain stable;
- no package dependency changes;
- all targeted and full validation commands pass.
