# Scoped IR slots plan

## Status

Deferred. The current lowering continues to represent a source read-only local declared inside a loop with a mutable function-scoped IR slot.

## Motivation

IR slots currently belong to a function and retain one identity for the complete function invocation. A source local declared inside a loop instead denotes a new lexical instance on every execution of its declaration.

Lowering such a read-only local to a mutable slot preserves runtime behavior but loses its source-level capability in the portable IR. Scoped slots would let the IR represent the renewed identity directly while still permitting exporters to reuse physical storage.

Associating a slot directly with one basic block is insufficient:

- one lexical scope may lower to multiple basic blocks;
- a basic block may execute repeatedly;
- definitions and uses may cross basic-block boundaries within the same lexical scope;
- branches, `break`, `continue`, and `return` produce multiple exits from one scope.

The IR therefore needs explicit lifetime regions rather than basic-block-local slot tables.

## Objectives

- Preserve `IrSlotMutability.ReadOnly` for source read-only locals declared in re-entered scopes.
- Represent a new semantic slot instance whenever control enters a new lifetime-region activation.
- Keep slot identifiers function-global so existing instruction operands remain simple integer references.
- Permit physical storage reuse when a scoped slot cannot escape its region.
- Validate visibility, definite assignment, and read-only assignment per region activation.
- Preserve deterministic MuIR serialization.

## Non-objectives

- Do not introduce heap allocation for every loop iteration.
- Do not make slots first-class runtime values.
- Do not expose lexical scopes to provider functions or runtime reflection.
- Do not convert the portable IR to SSA.
- Do not require exporters to preserve source variable names or debugger lifetimes.

## Proposed model

### Function root region

Every function has an implicit root lifetime region. Parameters, temporaries, and existing function-scoped locals belong to this region.

MuIR version 1 documents map all slots to the implicit root region.

### Nested lifetime regions

An `IrLifetimeRegion` describes a single-entry, potentially multi-exit set of basic blocks nested within another region.

Each region records:

- a function-unique region identifier;
- its parent region identifier;
- its entry block;
- the basic blocks directly owned by the region.

Every basic block belongs directly to one region. A region contains the blocks it owns and all blocks owned by its descendants.

Control may enter a non-root region only through its entry block from its parent region. An edge from inside a region back to its entry block is invalid because it would ambiguously mix re-entry with an intra-activation loop. Lowering must route re-entry through a block in the parent region.

### Scoped slots

Each `IrSlot` records its owning lifetime-region identifier.

Slot identifiers remain contiguous and function-global. Instructions continue to reference slots by the existing integer slot ID.

A slot is visible in blocks owned by its region or descendants. It is not visible in its parent, sibling regions, or blocks reached after the region has exited.

Entering a region creates a new semantic instance of every slot owned by that region. Leaving the region ends those instances. A later entry creates new instances even when an exporter reuses the same physical storage.

### Read-only definitions

Read-only validation operates per region activation:

- a read-only parameter is defined by function entry and cannot be an instruction destination;
- a read-only local may remain undefined when it is never read;
- mutually exclusive definitions are valid;
- no path within one activation may execute more than one definition;
- leaving and re-entering the owning region resets the definition state for the new instance.

A cycle contained within a region must therefore reject a repeated read-only definition. A cycle that exits the region and re-enters through its entry creates a new activation and may execute the definition again.

### Physical storage

Exporters may allocate one physical location per function-global slot ID and reuse it across region activations. Scoped-slot semantics do not require per-iteration heap allocation because slots cannot escape their lifetime region.

An exporter may clear reused physical storage on region entry for defensive isolation. Correctness must not depend on clearing because validation prohibits reads before definition in each activation.

## Public IR API changes

Introduce an `IrLifetimeRegion` public record in its own file.

Extend `IrFunction` with a region collection. The root region must be identifiable without relying on collection order.

Extend `IrSlot` with its owning region identifier while retaining convenience construction for root-region slots.

Keep block and slot IDs function-global and contiguous. Region IDs should follow the same contiguous, zero-based convention unless implementation constraints justify a different invariant.

Update public API baselines and XML documentation.

## Lowering changes

Extend `IrBuilder` to:

- create lifetime regions;
- track the active region while creating blocks and slots;
- assign each block and slot to its active region;
- validate region-stack transitions during construction.

Extend `Lowerer` to create a re-enterable region for a lexical scope whose declaration may execute more than once. The initial implementation should cover loop bodies and any nested block containing locals.

Lowering must shape loop control flow so:

- entry into the loop-body region comes from a parent-region block;
- `continue` exits the body region before reaching the condition or iterator;
- the next iteration re-enters through the region entry;
- `break` and `return` exit all applicable nested regions;
- branches contained in the body remain in the same activation unless they enter nested regions.

After this model is available, remove the `DeclarationLoopDepth == 0` condition from local-slot mutability selection and preserve source read-only capability for loop-local declarations.

## Validator changes

Add structural validation for:

- contiguous and unique region IDs;
- one root region per function;
- valid parent references with no region-parent cycles;
- one direct owner for every basic block;
- valid entry blocks;
- single-entry nested regions;
- no control-flow edge into a region except through its entry;
- no internal edge to a nested region entry that would imply an activation reset;
- slot ownership by an existing region;
- parameter ownership by the root region;
- slot use only while its owning region is active.

Replace the current function-wide read-only definition analysis with region-aware dataflow. Definition state for slots owned by a region starts empty on every region entry and is discarded on exit. Parent-region slot state flows through child-region activations unchanged except for valid definitions of slots owned by the parent.

Extend definite-assignment validation with the same activation semantics so stale physical values from an earlier activation cannot justify a read.

## MuIR format changes

Scoped slots require a new MuIR format version because the function and slot grammar changes.

The new version should serialize:

- the region count in each function header;
- each region's ID, parent, entry block, and directly owned blocks;
- each slot's owning region.

The writer must emit regions, slots, and blocks in deterministic identifier order.

The reader should continue supporting MuIR version 1 by synthesizing one root region and assigning every slot and block to it. The writer may either always emit the new version or retain version 1 output when no nested region exists; this choice must be made before implementation.

Update reader limits to include a maximum region count and validate all references before constructing an accepted program.

## .NET exporter changes

Continue creating one `ParameterExpression` per function-global slot ID.

Add region-entry handling only if physical clearing is selected. No dynamic object array or heap cell is required for each activation.

Ensure generated jumps follow the validated region transitions. The exporter must not independently reconstruct lexical scopes or repeat region validation.

## Test plan

Add IR construction and validator tests covering:

- root-region compatibility with existing programs;
- a read-only scoped local defined once per loop iteration;
- rejection of two definitions in one activation;
- mutually exclusive definitions within one activation;
- use before definition after re-entry;
- use outside the owning region;
- illegal entry into a nested region;
- illegal internal jump to a region entry;
- nested loop regions;
- `break`, `continue`, and `return` exits;
- parent-region locals used and assigned inside child regions;
- malformed region parent graphs.

Add MuIR tests covering:

- deterministic round-trip of nested regions;
- version 1 documents synthesized into a root region;
- invalid region, block, parent, and slot references;
- configured region-count limits.

Add compiler and exporter tests proving that a read-only local declared inside a loop remains read-only in IR and executes correctly over multiple iterations.

## Documentation updates

Update:

- the portable IR specification;
- the MuIR serialization specification;
- public API documentation;
- the detailed changelog;
- any diagrams or examples that describe function-scoped slot spaces.

Document the distinction between semantic slot instances and reusable physical exporter storage.

## Implementation sequence

1. Finalize region invariants and MuIR versioning.
2. Add the public region model and root-region compatibility constructors.
3. Extend structural validation without changing lowering.
4. Add MuIR read/write support and compatibility tests.
5. Add region construction to `IrBuilder`.
6. Lower loop-contained lexical scopes into re-enterable regions.
7. Implement region-aware definite-assignment and read-only validation.
8. Preserve read-only mutability for loop-local source variables.
9. Update the .NET exporter and runtime tests.
10. Update specifications, API baselines, and changelog.

## Decisions required before implementation

- Name the abstraction `scope`, `lifetime region`, or another term.
- Decide whether every lexical block receives a region or only re-enterable scopes.
- Decide whether MuIR writers always emit the new version.
- Decide whether exporters clear physical storage on region entry.
- Decide whether region ownership is stored on blocks, regions, or redundantly on both with validation.
