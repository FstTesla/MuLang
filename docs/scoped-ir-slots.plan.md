# Scoped IR slots plan

## Status

Planned for `0.3.0-alpha.3` together with the MuIR 2 exception-region model.
The current lowering continues to represent a source read-only local declared
inside a loop with a mutable function-scoped IR slot.

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

### Relationship to exception regions

Lifetime regions and exception regions form distinct, coordinated hierarchies.
Lifetime regions define slot visibility and activation. Exception regions
define protected execution, handlers, cleanup, and exceptional control flow.

Every basic block records exactly one lifetime-region owner. Exception regions
instead list the blocks directly owned by each protected, handler, or cleanup
part. A block has at most one direct exception-region owner; membership in
outer exception regions follows the exception-region parent chain.

The boundaries of the two hierarchies are independent. An exception-region
part may cover part of a lifetime region and may contain nested lifetime
regions. A handler has a dedicated lifetime region because it owns the caught
error slot. A cleanup receives a dedicated lifetime region only when it owns
local slots.

### Function root region

Every function has an explicit root lifetime region with ID `0`. Its entry is
the function entry block. Parameters and function-lifetime locals belong to
this region.

MuIR version 1 documents map all slots and blocks to a synthesized root region
with ID `0`.

### Nested lifetime regions

An `IrLifetimeRegion` describes a single-entry, potentially multi-exit set of basic blocks nested within another region.

Each region records:

- a function-unique region identifier;
- its parent region identifier;
- its entry block.

Lifetime-region IDs are contiguous and zero-based within each function.
Exception-region IDs use a separate contiguous, zero-based namespace.

Every basic block records one lifetime-region ID. A region contains the blocks
it owns and all blocks owned by its descendants.

Control may enter a non-root region only through its entry block from its
parent region. An edge from inside a region back to its entry block is invalid
because it would ambiguously mix re-entry with an intra-activation loop.
Lowering must exit to a block in the parent region before re-entering.
Validated exceptional transfers may activate dedicated handler or cleanup
regions through their declared entries.

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

Exporters do not clear reused physical storage on region entry. Correctness
depends exclusively on validation prohibiting reads before definition in each
activation.

## Public IR API changes

Introduce an `IrLifetimeRegion` public record in its own file.

Extend `IrFunction` with a lifetime-region collection. Existing constructors
synthesize root region `0` and an empty exception-region collection.

Extend `IrSlot` and `IrBasicBlock` with their owning lifetime-region identifier.
Existing constructors continue assigning root region `0`.

Keep block and slot IDs function-global and contiguous. Lifetime-region and
exception-region IDs are independently contiguous and zero-based.

Update public API baselines and XML documentation.

## Lowering changes

Extend `IrBuilder` to:

- create lifetime regions;
- maintain a lifetime-region stack independently from the exception-region
  stack;
- track the active region while creating blocks and slots;
- assign each block and slot to its active region;
- validate region-stack transitions during construction.

Extend `Lowerer` to create a lifetime region for every lexical scope that owns
at least one local slot. Lexical scopes without slots do not produce regions.

Compiler temporaries belong to the active region or the nearest ancestor
required by all their uses. Parameters belong to the root region.

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

Read-only validation is path-sensitive within one activation. Mutually
exclusive definitions are valid, but no path may define a read-only slot twice
or read it before definition. A read-only slot need not be defined on paths
where it is never read.

## MuIR format changes

Scoped slots extend MuIR version 2 because that format has not entered a stable
release. Existing prerelease MuIR 2 documents are intentionally incompatible
and rejected. MuIR version 1 remains readable through root-region synthesis.

Every MuIR 2 function serializes:

- the lifetime-region and exception-region counts;
- every lifetime region's ID, parent, and entry block;
- every block's lifetime-region owner;
- each slot's owning region.

The writer always emits MuIR version 2, including root region `0` and zero
counts for empty exception-region collections. It emits regions, slots, and
blocks in deterministic identifier order.

The reader continues supporting MuIR version 1 by synthesizing one root region
and assigning every slot and block to it.

Update reader limits with separate positive maxima for lifetime regions and
exception regions. Validate all references before constructing an accepted
program.

## .NET exporter changes

Continue creating one `ParameterExpression` per function-global slot ID.

No region-entry clearing, dynamic object array, or per-activation heap cell is
required.

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

1. Add `IrLifetimeRegion`, root-region compatibility constructors, and lifetime
   ownership to slots and blocks.
2. Extend structural validation without changing lowering.
3. Add the revised MuIR 2 read/write support, MuIR 1 compatibility, limits, and
   round-trip tests.
4. Add structured lifetime-region construction to `IrBuilder`.
5. Lower every lexical scope with local slots into a lifetime region.
6. Assign temporaries to the active or minimum required ancestor region.
7. Implement region-aware definite-assignment and path-sensitive read-only
   validation.
8. Preserve read-only mutability for loop-local source variables.
9. Update the .NET exporter and runtime tests without storage clearing.
10. Implement the coordinated exception-region work after the lifetime model
    is complete, without publishing an intermediate MuIR 2 contract.
11. Update specifications, API baselines, roadmap, and changelog for
    `0.3.0-alpha.3`.
