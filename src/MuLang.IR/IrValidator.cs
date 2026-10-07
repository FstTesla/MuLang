using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Collections.ObjectModel;

namespace MuLang.IR;

/// <summary>Validates portable MuLang IR against its structural and environment contracts.</summary>
public static class IrValidator
{
    /// <summary>Validates a program against an environment.</summary>
    /// <param name="program">The IR program to validate.</param>
    /// <param name="environment">The expected provider environment.</param>
    /// <returns>The validation diagnostics.</returns>
    public static DiagnosticCollection Validate(
        IrProgram program,
        EnvironmentSchema environment
    )
    {
        return Validate(program, environment, null, null);
    }

    /// <summary>Validates a program against an environment and expected compilation metadata.</summary>
    /// <param name="program">The IR program to validate.</param>
    /// <param name="environment">The expected provider environment.</param>
    /// <param name="expectedCompilationMode">The expected compilation mode, or <c>null</c> to skip this check.</param>
    /// <param name="expectedLanguageProfileFingerprint">The expected profile fingerprint, or <c>null</c> to skip this check.</param>
    /// <returns>The validation diagnostics.</returns>
    public static DiagnosticCollection Validate(
        IrProgram program,
        EnvironmentSchema environment,
        CompilationMode? expectedCompilationMode,
        LanguageProfileFingerprint? expectedLanguageProfileFingerprint
    )
    {
        if (program is null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        ICollection<Diagnostic> diagnostics = [ ];

        if (!Enum.IsDefined(program.CompilationMode))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.CompilationMetadataMismatch,
                default,
                "The IR compilation mode is invalid."
            );
        }

        if (program.EnvironmentFingerprint != environment.Fingerprint)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.EnvironmentMismatch,
                default,
                "The IR environment fingerprint does not match the supplied environment."
            );
        }

        if (
            expectedCompilationMode is not null &&
            program.CompilationMode != expectedCompilationMode
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.CompilationMetadataMismatch,
                default,
                "The IR compilation mode does not match the expected compilation mode."
            );
        }

        if (
            expectedLanguageProfileFingerprint is not null &&
            program.LanguageProfileFingerprint != expectedLanguageProfileFingerprint
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.CompilationMetadataMismatch,
                default,
                "The IR language profile fingerprint does not match the expected language profile."
            );
        }

        ISet<string> functionIds = new HashSet<string>(StringComparer.Ordinal)
        {
            program.EntryFunction.Id,
        };

        foreach (IrFunction function in program.UserFunctions)
        {
            if (!functionIds.Add(function.Id))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR function '{function.Id}' is declared more than once."
                );
            }
        }

        ValidateFunction(program, program.EntryFunction, environment, diagnostics);

        foreach (IrFunction function in program.UserFunctions)
        {
            ValidateFunction(program, function, environment, diagnostics);
        }

        return DiagnosticCollection.Create(diagnostics);
    }

    private static void ValidateFunction(
        IrProgram program,
        IrFunction function,
        EnvironmentSchema environment,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrProgram functionProgram = new (
            program.EnvironmentFingerprint,
            program.CompilationMode,
            program.LanguageProfileFingerprint,
            function,
            program.UserFunctions
        );
        IReadOnlyDictionary<int, IrBasicBlock> blocksById = ValidateBlocks(
            functionProgram,
            diagnostics
        );
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById =
            ValidateLifetimeRegions(function, blocksById, diagnostics);
        IReadOnlyDictionary<int, ExceptionBlockOwner> exceptionOwners =
            ValidateExceptionRegions(
                function,
                blocksById,
                lifetimeRegionsById,
                diagnostics
            );
        ValidateSlots(functionProgram, lifetimeRegionsById, diagnostics);

        if (!blocksById.ContainsKey(functionProgram.EntryBlock))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidStructure,
                default,
                $"Entry block {functionProgram.EntryBlock} does not exist in function '{function.Id}'."
            );
        }

        ValidateParameterSlots(
            function,
            ReferenceEquals(function, program.EntryFunction),
            diagnostics
        );

        foreach (IrBasicBlock block in functionProgram.Blocks)
        {
            ValidateBlock(functionProgram, environment, blocksById, block, diagnostics);
            ValidateSlotVisibility(
                functionProgram,
                lifetimeRegionsById,
                block,
                diagnostics
            );
        }

        ValidateLifetimeRegionEdges(
            function,
            blocksById,
            lifetimeRegionsById,
            diagnostics
        );
        ValidateExceptionRegionControlFlow(
            function,
            blocksById,
            exceptionOwners,
            diagnostics
        );
        ValidateReadOnlySlots(
            functionProgram,
            blocksById,
            lifetimeRegionsById,
            diagnostics
        );
        ValidateDefinitions(
            functionProgram,
            blocksById,
            lifetimeRegionsById,
            diagnostics
        );
    }

    private static void ValidateParameterSlots(
        IrFunction function,
        bool isEntryFunction,
        ICollection<Diagnostic> diagnostics
    )
    {
        bool hasNonParameter = false;
        int parameterCount = 0;

        foreach (IrSlot slot in function.Slots)
        {
            if (slot.Kind == IrSlotKind.Parameter)
            {
                parameterCount++;

                if (hasNonParameter)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidSlot,
                        default,
                        $"Parameter slot {slot.Id} in function '{function.Id}' is not contiguous."
                    );
                }
            }
            else
            {
                hasNonParameter = true;
            }
        }

        if (isEntryFunction && parameterCount != 0)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidSlot,
                default,
                "The IR entry function cannot declare parameter slots."
            );
        }
    }

    private static void ValidateSlots(
        IrProgram program,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById,
        ICollection<Diagnostic> diagnostics
    )
    {
        for (int index = 0; index < program.Slots.Count; index++)
        {
            IrSlot slot = program.Slots[index];

            if (slot.Id != index)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR slot at index {index} has identifier {slot.Id}."
                );
            }

            if (slot.Type.Kind is TypeKind.Void or TypeKind.Null or TypeKind.ErrorRecovery)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR slot {slot.Id} has invalid type '{slot.Type.DisplayName}'."
                );
            }

            if (!Enum.IsDefined(slot.Mutability))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR slot {slot.Id} has invalid mutability."
                );
            }
            else if (slot.Kind == IrSlotKind.Parameter)
            {
                if (slot.Mutability != IrSlotMutability.ReadOnly)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidSlot,
                        default,
                        $"IR parameter slot {slot.Id} must be read-only."
                    );
                }
            }
            else if (slot is { Kind: IrSlotKind.Temporary, Mutability: IrSlotMutability.ReadOnly })
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR temporary slot {slot.Id} cannot be read-only."
                );
            }

            if (!lifetimeRegionsById.ContainsKey(slot.LifetimeRegion))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR slot {slot.Id} belongs to undefined lifetime region {slot.LifetimeRegion}."
                );
            }
            else if (
                slot.Kind == IrSlotKind.Parameter &&
                slot.LifetimeRegion != 0
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR parameter slot {slot.Id} must belong to lifetime root region 0."
                );
            }
        }
    }

    private static void ValidateReadOnlySlots(
        IrProgram program,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (!blocksById.ContainsKey(program.EntryBlock))
        {
            return;
        }

        IReadOnlyCollection<int> reachable = GetReachableBlocks(
            program.EntryBlock,
            blocksById
        );
        IReadOnlyDictionary<int, IReadOnlyList<int>> predecessors = GetPredecessors(
            reachable,
            blocksById
        );
        IReadOnlySet<int> entryDefinitions = new HashSet<int>(
            program.Slots
                .Where(
                    static slot =>
                        slot is { Kind: IrSlotKind.Parameter, Mutability: IrSlotMutability.ReadOnly }
                )
                .Select(static slot => slot.Id)
        );
        IReadOnlySet<int> readOnlySlots = new HashSet<int>(
            program.Slots
                .Where(static slot => slot.Mutability == IrSlotMutability.ReadOnly)
                .Select(static slot => slot.Id)
        );
        Dictionary<int, IReadOnlySet<int>> outgoing = reachable.ToDictionary(
            static blockId => blockId,
            static IReadOnlySet<int> (_) => ReadOnlySet<int>.Empty
        );
        bool changed;

        do
        {
            changed = false;

            foreach (int blockId in reachable.Order())
            {
                IReadOnlySet<int> incoming = GetIncomingPossibleDefinitions(
                    program,
                    blockId,
                    program.EntryBlock,
                    predecessors,
                    outgoing,
                    entryDefinitions,
                    blocksById,
                    lifetimeRegionsById
                );
                HashSet<int> definitions = [ .. incoming ];

                foreach (IrInstruction instruction in blocksById[blockId].Instructions)
                {
                    int? destination = GetDestination(instruction);

                    if (
                        destination is not null &&
                        readOnlySlots.Contains(destination.Value)
                    )
                    {
                        definitions.Add(destination.Value);
                    }
                }

                if (!outgoing[blockId].SetEquals(definitions))
                {
                    outgoing[blockId] = definitions;
                    changed = true;
                }
            }
        }
        while (changed);

        foreach (int blockId in reachable)
        {
            ISet<int> defined = new HashSet<int>(
                GetIncomingPossibleDefinitions(
                    program,
                    blockId,
                    program.EntryBlock,
                    predecessors,
                    outgoing,
                    entryDefinitions,
                    blocksById,
                    lifetimeRegionsById
                )
            );

            foreach (IrInstruction instruction in blocksById[blockId].Instructions)
            {
                int? destination = GetDestination(instruction);

                if (
                    destination is null ||
                    !readOnlySlots.Contains(destination.Value)
                )
                {
                    continue;
                }

                IrSlot? slot = GetSlot(program, destination.Value);

                if (
                    slot is null ||
                    slot.Mutability != IrSlotMutability.ReadOnly
                )
                {
                    continue;
                }

                if (
                    slot.Kind == IrSlotKind.Parameter ||
                    defined.Contains(destination.Value)
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidSlot,
                        instruction.Span,
                        slot.Kind == IrSlotKind.Parameter
                            ? $"Read-only IR parameter slot {slot.Id} cannot have a definition site."
                            : $"Read-only IR local slot {slot.Id} may be defined more than once on a control-flow path."
                    );
                }

                defined.Add(destination.Value);
            }
        }
    }

    private static IReadOnlySet<int> GetIncomingPossibleDefinitions(
        IrProgram program,
        int blockId,
        int entryBlock,
        IReadOnlyDictionary<int, IReadOnlyList<int>> predecessors,
        IReadOnlyDictionary<int, IReadOnlySet<int>> outgoing,
        IReadOnlySet<int> entryDefinitions,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById
    )
    {
        HashSet<int> incoming =
            blockId == entryBlock
                ? [ .. entryDefinitions ]
                : [ ];

        foreach (int predecessor in predecessors[blockId])
        {
            incoming.UnionWith(
                FilterDefinitionsForRegion(
                    program,
                    outgoing[predecessor],
                    blocksById[blockId].LifetimeRegion,
                    lifetimeRegionsById
                )
            );
        }

        return incoming;
    }

    private static IReadOnlyDictionary<int, IrLifetimeRegion> ValidateLifetimeRegions(
        IrFunction function,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        ICollection<Diagnostic> diagnostics
    )
    {
        Dictionary<int, IrLifetimeRegion> regionsById = [ ];

        for (int index = 0; index < function.LifetimeRegions.Count; index++)
        {
            IrLifetimeRegion region = function.LifetimeRegions[index];

            if (region.Id != index)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR lifetime region at index {index} has identifier {region.Id}."
                );
            }

            if (!regionsById.TryAdd(region.Id, region))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR lifetime region {region.Id} is declared more than once."
                );
            }

            if (region.Id == 0)
            {
                if (region.ParentRegion is not null)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        default,
                        "IR lifetime root region 0 cannot have a parent."
                    );
                }

                if (region.EntryBlock != function.EntryBlock)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        default,
                        $"IR lifetime root region 0 must enter at function entry block {function.EntryBlock}."
                    );
                }
            }
            else if (
                region.ParentRegion is not int parentRegion ||
                parentRegion < 0 ||
                parentRegion >= region.Id
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR lifetime region {region.Id} has an invalid parent."
                );
            }

            if (!blocksById.ContainsKey(region.EntryBlock))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR lifetime region {region.Id} entry block {region.EntryBlock} does not exist."
                );
            }
        }

        if (!regionsById.ContainsKey(0))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidStructure,
                default,
                "IR function must declare lifetime root region 0."
            );
        }

        foreach (IrBasicBlock block in function.Blocks)
        {
            if (!regionsById.TryGetValue(
                    block.LifetimeRegion,
                    out IrLifetimeRegion? owner
                ))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    block.Terminator.Span,
                    $"IR block {block.Id} belongs to undefined lifetime region {block.LifetimeRegion}."
                );
            }
            else if (
                owner.EntryBlock == block.Id &&
                owner.Id != block.LifetimeRegion
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    block.Terminator.Span,
                    $"IR lifetime region {owner.Id} entry block {block.Id} has a different owner."
                );
            }
        }

        foreach (IrLifetimeRegion region in function.LifetimeRegions)
        {
            if (
                blocksById.TryGetValue(
                    region.EntryBlock,
                    out IrBasicBlock? entry
                ) &&
                entry.LifetimeRegion != region.Id
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    entry.Terminator.Span,
                    $"IR lifetime region {region.Id} entry block {entry.Id} must be owned by that region."
                );
            }
        }

        return regionsById;
    }

    private static void ValidateLifetimeRegionEdges(
        IrFunction function,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById,
        ICollection<Diagnostic> diagnostics
    )
    {
        foreach (IrBasicBlock source in function.Blocks)
        {
            foreach (int successorId in GetSuccessors(source.Terminator))
            {
                if (
                    !blocksById.TryGetValue(successorId, out IrBasicBlock? target) ||
                    !lifetimeRegionsById.TryGetValue(
                        source.LifetimeRegion,
                        out IrLifetimeRegion? sourceRegion
                    ) ||
                    !lifetimeRegionsById.TryGetValue(
                        target.LifetimeRegion,
                        out IrLifetimeRegion? targetRegion
                    )
                )
                {
                    continue;
                }

                bool remainsActive = IsRegionAncestor(
                    targetRegion.Id,
                    sourceRegion.Id,
                    lifetimeRegionsById
                );
                bool entersChild =
                    targetRegion.ParentRegion == sourceRegion.Id &&
                    targetRegion.EntryBlock == target.Id;

                if (!remainsActive && !entersChild)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        source.Terminator.Span,
                        $"IR edge from block {source.Id} to block {target.Id} enters lifetime region {targetRegion.Id} illegally."
                    );
                }
                else if (
                    sourceRegion.Id == targetRegion.Id &&
                    targetRegion.Id != 0 &&
                    targetRegion.EntryBlock == target.Id
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        source.Terminator.Span,
                        $"IR edge from block {source.Id} to lifetime region {targetRegion.Id} entry block {target.Id} would restart an active region."
                    );
                }
            }
        }
    }

    private static IReadOnlyDictionary<int, ExceptionBlockOwner>
        ValidateExceptionRegions(
            IrFunction function,
            IReadOnlyDictionary<int, IrBasicBlock> blocksById,
            IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById,
            ICollection<Diagnostic> diagnostics
        )
    {
        Dictionary<int, ExceptionBlockOwner> owners = [ ];

        for (int index = 0; index < function.ExceptionRegions.Count; index++)
        {
            IrExceptionRegion region = function.ExceptionRegions[index];

            if (region.Id != index)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR exception region at index {index} has identifier {region.Id}."
                );
            }

            if (
                region.Id == 0
                    ? region.ParentRegion is not null || region.ParentPart is not null
                    : region.ParentRegion is not int parentRegion ||
                    parentRegion < 0 ||
                    parentRegion >= region.Id ||
                    region.ParentPart is null
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR exception region {region.Id} has an invalid parent relationship."
                );
            }

            if (region.Handler is null && region.Cleanup is null)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR exception region {region.Id} must declare a handler or cleanup."
                );
            }

            AddComponent(
                region,
                IrExceptionRegionPart.Protected,
                region.Protected.EntryBlock,
                region.Protected.Blocks
            );

            if (region.Handler is IrExceptionHandler handler)
            {
                AddComponent(
                    region,
                    IrExceptionRegionPart.Handler,
                    handler.EntryBlock,
                    handler.Blocks
                );

                IrSlot? errorSlot = function.Slots.ElementAtOrDefault(
                    handler.ErrorSlot
                );

                if (
                    errorSlot is null ||
                    errorSlot.Id != handler.ErrorSlot ||
                    errorSlot.Kind != IrSlotKind.Local ||
                    errorSlot.Mutability != IrSlotMutability.ReadOnly ||
                    !TypeRelations.AreEquivalent(
                        errorSlot.Type,
                        TypeSymbols.ErrorValue
                    )
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidSlot,
                        default,
                        $"IR exception region {region.Id} handler error slot is invalid."
                    );
                }
                else if (
                    blocksById.TryGetValue(
                        handler.EntryBlock,
                        out IrBasicBlock? handlerEntry
                    ) &&
                    (
                        !lifetimeRegionsById.ContainsKey(
                            errorSlot.LifetimeRegion
                        ) ||
                        handlerEntry.LifetimeRegion != errorSlot.LifetimeRegion ||
                        errorSlot.LifetimeRegion == 0
                    )
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidSlot,
                        default,
                        $"IR exception region {region.Id} handler error slot must belong to the dedicated handler lifetime region."
                    );
                }
            }

            if (region.Cleanup is IrExceptionCleanup cleanup)
            {
                AddComponent(
                    region,
                    IrExceptionRegionPart.Cleanup,
                    cleanup.EntryBlock,
                    cleanup.Blocks
                );
            }

            if (
                region.ParentRegion is int parentId &&
                parentId < function.ExceptionRegions.Count &&
                region.ParentPart is IrExceptionRegionPart parentPart &&
                !HasComponent(function.ExceptionRegions[parentId], parentPart)
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR exception region {region.Id} refers to a missing parent component."
                );
            }
        }

        return owners;

        void AddComponent(
            IrExceptionRegion region,
            IrExceptionRegionPart part,
            int entryBlock,
            IReadOnlyCollection<int> componentBlocks
        )
        {
            if (componentBlocks.Count == 0 || !componentBlocks.Contains(entryBlock))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR exception region {region.Id} {part.ToString().ToLowerInvariant()} component has an invalid entry or no blocks."
                );
            }

            if (componentBlocks.Count != componentBlocks.Distinct().Count())
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR exception region {region.Id} {part.ToString().ToLowerInvariant()} component contains duplicate blocks."
                );
            }

            foreach (int blockId in componentBlocks)
            {
                if (!blocksById.ContainsKey(blockId))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        default,
                        $"IR exception region {region.Id} refers to undefined block {blockId}."
                    );
                }
                else if (!owners.TryAdd(
                        blockId,
                        new ExceptionBlockOwner(region.Id, part)
                    ))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        blocksById[blockId].Terminator.Span,
                        $"IR block {blockId} has more than one direct exception-region owner."
                    );
                }
            }
        }
    }

    private static void ValidateExceptionRegionControlFlow(
        IrFunction function,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IReadOnlyDictionary<int, ExceptionBlockOwner> owners,
        ICollection<Diagnostic> diagnostics
    )
    {
        foreach (IrBasicBlock source in function.Blocks)
        {
            owners.TryGetValue(source.Id, out ExceptionBlockOwner? sourceOwner);

            if (
                source.Terminator is IrTerminator.Return &&
                sourceOwner?.Part == IrExceptionRegionPart.Cleanup
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    source.Terminator.Span,
                    $"IR cleanup block {source.Id} cannot return."
                );
            }

            if (
                source.Terminator is IrTerminator.Resume &&
                sourceOwner?.Part != IrExceptionRegionPart.Cleanup
            )
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    source.Terminator.Span,
                    $"IR resume terminator in block {source.Id} is outside cleanup."
                );
            }

            foreach (int successorId in GetSuccessors(source.Terminator))
            {
                if (!blocksById.ContainsKey(successorId))
                {
                    continue;
                }

                owners.TryGetValue(
                    successorId,
                    out ExceptionBlockOwner? targetOwner
                );

                if (
                    targetOwner?.Part is
                        IrExceptionRegionPart.Handler or
                        IrExceptionRegionPart.Cleanup &&
                    !Equals(targetOwner, sourceOwner)
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        source.Terminator.Span,
                        $"IR edge from block {source.Id} enters a handler or cleanup component directly."
                    );
                }

                if (
                    sourceOwner?.Part == IrExceptionRegionPart.Cleanup &&
                    !Equals(targetOwner, sourceOwner)
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        source.Terminator.Span,
                        $"IR cleanup block {source.Id} cannot transfer outside its cleanup component."
                    );
                }
            }
        }
    }

    private static bool HasComponent(
        IrExceptionRegion region,
        IrExceptionRegionPart part
    )
    {
        return part switch
        {
            IrExceptionRegionPart.Protected => true,
            IrExceptionRegionPart.Handler => region.Handler is not null,
            IrExceptionRegionPart.Cleanup => region.Cleanup is not null,
            _ => false,
        };
    }

    private static void ValidateSlotVisibility(
        IrProgram program,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById,
        IrBasicBlock block,
        ICollection<Diagnostic> diagnostics
    )
    {
        foreach (IrInstruction instruction in block.Instructions)
        {
            int? destination = GetDestination(instruction);

            if (destination is not null)
            {
                ValidateSlotVisibility(
                    program,
                    lifetimeRegionsById,
                    block,
                    destination.Value,
                    instruction.Span,
                    diagnostics
                );
            }

            foreach (int operand in GetOperands(instruction))
            {
                ValidateSlotVisibility(
                    program,
                    lifetimeRegionsById,
                    block,
                    operand,
                    instruction.Span,
                    diagnostics
                );
            }
        }

        foreach (int operand in GetOperands(block.Terminator))
        {
            ValidateSlotVisibility(
                program,
                lifetimeRegionsById,
                block,
                operand,
                block.Terminator.Span,
                diagnostics
            );
        }
    }

    private static void ValidateSlotVisibility(
        IrProgram program,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById,
        IrBasicBlock block,
        int slotId,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? slot = GetSlot(program, slotId);

        if (
            slot is not null &&
            lifetimeRegionsById.ContainsKey(slot.LifetimeRegion) &&
            lifetimeRegionsById.ContainsKey(block.LifetimeRegion) &&
            !IsRegionAncestor(
                slot.LifetimeRegion,
                block.LifetimeRegion,
                lifetimeRegionsById
            )
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidSlot,
                span,
                $"IR slot {slot.Id} is not visible in block {block.Id}."
            );
        }
    }

    private static Dictionary<int, IrBasicBlock> ValidateBlocks(
        IrProgram program,
        ICollection<Diagnostic> diagnostics
    )
    {
        Dictionary<int, IrBasicBlock> blocksById = [ ];

        for (int index = 0; index < program.Blocks.Count; index++)
        {
            IrBasicBlock block = program.Blocks[index];

            if (block.Id != index)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    block.Terminator.Span,
                    $"IR block at index {index} has identifier {block.Id}."
                );
            }

            if (!blocksById.TryAdd(block.Id, block))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    block.Terminator.Span,
                    $"IR block {block.Id} is declared more than once."
                );
            }
        }

        return blocksById;
    }

    private static void ValidateBlock(
        IrProgram program,
        EnvironmentSchema environment,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IrBasicBlock block,
        ICollection<Diagnostic> diagnostics
    )
    {
        foreach (IrInstruction instruction in block.Instructions)
        {
            ValidateInstruction(program, environment, instruction, diagnostics);
        }

        switch (block.Terminator)
        {
            case IrTerminator.Jump jump:
            {
                ValidateBlockTarget(blocksById, jump.TargetBlock, jump.Span, diagnostics);
                break;
            }

            case IrTerminator.Branch branch:
            {
                ValidateSlotType(
                    program,
                    branch.Condition,
                    TypeSymbols.Bool,
                    branch.Span,
                    diagnostics
                );
                ValidateBlockTarget(blocksById, branch.TrueBlock, branch.Span, diagnostics);
                ValidateBlockTarget(blocksById, branch.FalseBlock, branch.Span, diagnostics);
                break;
            }

            case IrTerminator.Return result:
            {
                if (program.ResultType.Kind == TypeKind.Void)
                {
                    if (result.Value is not null)
                    {
                        Report(
                            diagnostics,
                            IrDiagnosticCodes.TypeMismatch,
                            result.Span,
                            "A void IR program cannot return a value."
                        );
                    }
                }
                else if (result.Value is null)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        result.Span,
                        $"IR program must return '{program.ResultType.DisplayName}'."
                    );
                }
                else
                {
                    ValidateSlotType(
                        program,
                        result.Value.Value,
                        program.ResultType,
                        result.Span,
                        diagnostics
                    );
                }

                break;
            }

            case IrTerminator.Throw error:
            {
                ValidateSlotType(
                    program,
                    error.Error,
                    TypeSymbols.ErrorValue,
                    error.Span,
                    diagnostics
                );
                break;
            }

            case IrTerminator.Resume:
            {
                break;
            }
        }
    }

    private static void ValidateInstruction(
        IrProgram program,
        EnvironmentSchema environment,
        IrInstruction instruction,
        ICollection<Diagnostic> diagnostics
    )
    {
        switch (instruction)
        {
            case IrInstruction.Constant constant:
            {
                ValidateSlotType(
                    program,
                    constant.Destination,
                    constant.Type,
                    constant.Span,
                    diagnostics
                );

                if (!IsConstantCompatible(constant.Value, constant.Type))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        constant.Span,
                        $"IR constant is incompatible with '{constant.Type.DisplayName}'."
                    );
                }

                break;
            }

            case IrInstruction.Copy copy:
            {
                ValidateEquivalentSlots(
                    program,
                    copy.Destination,
                    copy.Source,
                    copy.Span,
                    diagnostics
                );
                break;
            }

            case IrInstruction.LoadGlobal global:
            {
                if (!environment.TryGetGlobalById(global.GlobalId, out GlobalSymbol? symbol))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.UndefinedProviderSymbol,
                        global.Span,
                        $"Global '{global.GlobalId}' is not declared."
                    );
                    break;
                }

                ValidateSlotType(
                    program,
                    global.Destination,
                    symbol.Type,
                    global.Span,
                    diagnostics
                );
                break;
            }

            case IrInstruction.Unary unary:
            {
                ValidateUnary(program, unary, diagnostics);
                break;
            }

            case IrInstruction.Binary binary:
            {
                ValidateBinary(program, binary, diagnostics);
                break;
            }

            case IrInstruction.Convert conversion:
            {
                ValidateSlotType(
                    program,
                    conversion.Destination,
                    conversion.TargetType,
                    conversion.Span,
                    diagnostics
                );
                IrSlot? sourceSlot = GetSlot(program, conversion.Source);

                if (sourceSlot is null)
                {
                    ValidateSlot(program, conversion.Source, conversion.Span, diagnostics);
                    break;
                }

                bool isValid = conversion.Kind switch
                {
                    IrConversionKind.CheckedCast =>
                        TypeRelations.IsCastable(
                            sourceSlot.Type,
                            conversion.TargetType
                        ),
                    IrConversionKind.ValueConversion =>
                        TypeRelations.ClassifyConversion(
                            sourceSlot.Type,
                            conversion.TargetType
                        ) != ConversionKind.None,
                    _ => false,
                };

                if (!isValid)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        conversion.Span,
                        $"IR conversion from '{sourceSlot.Type.DisplayName}' to '{conversion.TargetType.DisplayName}' is invalid."
                    );
                }

                break;
            }

            case IrInstruction.Truthiness truthiness:
            {
                ValidateSlotType(
                    program,
                    truthiness.Destination,
                    TypeSymbols.Bool,
                    truthiness.Span,
                    diagnostics
                );
                IrSlot? source = GetSlot(program, truthiness.Source);

                if (source is null)
                {
                    ValidateSlot(
                        program,
                        truthiness.Source,
                        truthiness.Span,
                        diagnostics
                    );
                    break;
                }

                if (source.Type.Kind == TypeKind.Void)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        truthiness.Span,
                        "IR truthiness source cannot have type 'void'."
                    );
                }

                break;
            }

            case IrInstruction.TypeTest typeTest:
            {
                ValidateSlotType(
                    program,
                    typeTest.Destination,
                    TypeSymbols.Bool,
                    typeTest.Span,
                    diagnostics
                );
                ValidateSlot(program, typeTest.Source, typeTest.Span, diagnostics);
                break;
            }

            case IrInstruction.IsNull isNull:
            {
                ValidateSlotType(
                    program,
                    isNull.Destination,
                    TypeSymbols.Bool,
                    isNull.Span,
                    diagnostics
                );
                ValidateSlot(program, isNull.Source, isNull.Span, diagnostics);
                break;
            }

            case IrInstruction.HasProperty propertyTest:
            {
                ValidateSlotType(
                    program,
                    propertyTest.Destination,
                    TypeSymbols.Bool,
                    propertyTest.Span,
                    diagnostics
                );
                ValidateSlot(program, propertyTest.Target, propertyTest.Span, diagnostics);
                ValidateSlotType(
                    program,
                    propertyTest.Key,
                    TypeSymbols.String,
                    propertyTest.Span,
                    diagnostics
                );
                break;
            }

            case IrInstruction.CreateArray array:
            {
                ValidateSlotType(
                    program,
                    array.Destination,
                    array.Type,
                    array.Span,
                    diagnostics
                );

                foreach (int element in array.Elements)
                {
                    ValidateSlotType(
                        program,
                        element,
                        array.Type.ElementType,
                        array.Span,
                        diagnostics
                    );
                }

                break;
            }

            case IrInstruction.CreateObject objectValue:
            {
                ValidateSlotType(
                    program,
                    objectValue.Destination,
                    objectValue.Type,
                    objectValue.Span,
                    diagnostics
                );

                foreach (IrInstruction.ObjectPropertyValue property in objectValue.Properties)
                {
                    if (
                        objectValue.Type.TryGetProperty(
                            property.Name,
                            out ObjectPropertySymbol? declaration
                        )
                    )
                    {
                        if (declaration.IsReadOnly)
                        {
                            ValidateSlotViewType(
                                program,
                                property.Value,
                                declaration.Type,
                                objectValue.Span,
                                diagnostics
                            );
                        }
                        else
                        {
                            ValidateSlotType(
                                program,
                                property.Value,
                                declaration.Type,
                                objectValue.Span,
                                diagnostics
                            );
                        }
                    }
                    else if (objectValue.Type.IsOpen)
                    {
                        ValidateSlotType(
                            program,
                            property.Value,
                            TypeSymbols.Nullable(TypeSymbols.Unknown),
                            objectValue.Span,
                            diagnostics
                        );
                    }
                    else
                    {
                        ValidateSlot(
                            program,
                            property.Value,
                            objectValue.Span,
                            diagnostics
                        );
                        Report(
                            diagnostics,
                            IrDiagnosticCodes.InvalidStructure,
                            objectValue.Span,
                            $"IR object creation supplies undeclared property '{property.Name}'."
                        );
                    }
                }

                if (
                    objectValue.Properties
                        .Select(static property => property.Name)
                        .Distinct(StringComparer.Ordinal)
                        .Count() != objectValue.Properties.Count
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        objectValue.Span,
                        "IR object creation contains duplicate property names."
                    );
                }

                IReadOnlySet<string> suppliedNames = objectValue.Properties
                    .Select(static property => property.Name)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (
                    ObjectPropertySymbol requiredProperty in objectValue.Type.Properties.Where(
                        static property => !property.IsOptional
                    )
                )
                {
                    if (!suppliedNames.Contains(requiredProperty.Name))
                    {
                        Report(
                            diagnostics,
                            IrDiagnosticCodes.InvalidStructure,
                            objectValue.Span,
                            $"IR object creation omits required property '{requiredProperty.Name}'."
                        );
                    }
                }

                break;
            }

            case IrInstruction.GetProperty property:
            {
                ValidatePropertyRead(program, property, diagnostics);
                break;
            }

            case IrInstruction.SetProperty property:
            {
                ValidatePropertyWrite(program, property, diagnostics);
                break;
            }

            case IrInstruction.RemoveProperty property:
            {
                ValidateSlot(program, property.Target, property.Span, diagnostics);
                IrSlot? target = GetSlot(program, property.Target);
                TypeSymbol? targetType = target is null
                    ? null
                    : GetNonNullable(target.Type);

                if (
                    targetType is ObjectTypeSymbol objectType &&
                    objectType.TryGetProperty(
                        property.Name,
                        out ObjectPropertySymbol? declaration
                    ) &&
                    (!declaration.IsOptional || declaration.IsReadOnly)
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        property.Span,
                        $"IR cannot remove property '{property.Name}' because it is required or read-only."
                    );
                }

                break;
            }

            case IrInstruction.GetElement element:
            {
                ValidateElementRead(program, element, diagnostics);
                break;
            }

            case IrInstruction.SetElement element:
            {
                ValidateElementWrite(program, element, diagnostics);
                break;
            }

            case IrInstruction.RemoveElementProperty property:
            {
                ValidateSlot(program, property.Target, property.Span, diagnostics);
                ValidateSlotType(
                    program,
                    property.Key,
                    TypeSymbols.String,
                    property.Span,
                    diagnostics
                );

                IrSlot? target = GetSlot(program, property.Target);

                if (
                    target is not null &&
                    GetNonNullable(target.Type) is ObjectTypeSymbol objectType &&
                    TryGetConstantString(
                        program,
                        property.Key,
                        out string propertyName
                    ) &&
                    objectType.TryGetProperty(
                        propertyName,
                        out ObjectPropertySymbol? declaration
                    ) &&
                    (!declaration.IsOptional || declaration.IsReadOnly)
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        property.Span,
                        $"IR cannot remove property '{propertyName}' because it is required or read-only."
                    );
                }

                break;
            }

            case IrInstruction.ProviderCall call:
            {
                ValidateProviderCall(program, environment, call, diagnostics);
                break;
            }

            case IrInstruction.UserCall call:
            {
                ValidateUserCall(program, call, diagnostics);
                break;
            }
        }
    }

    private static void ValidateUnary(
        IrProgram program,
        IrInstruction.Unary unary,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, unary.Destination);
        IrSlot? operand = GetSlot(program, unary.Operand);

        if (destination is null || operand is null)
        {
            ValidateSlot(program, unary.Destination, unary.Span, diagnostics);
            ValidateSlot(program, unary.Operand, unary.Span, diagnostics);
            return;
        }

        TypeSymbol? expectedType = unary.Operator switch
        {
            IrUnaryOperator.Identity or IrUnaryOperator.Negate
                when destination.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number =>
                destination.Type,
            IrUnaryOperator.LogicalNot => TypeSymbols.Bool,
            IrUnaryOperator.BitwiseNot => TypeSymbols.Int,
            _ => null,
        };

        if (
            expectedType is null ||
            !TypeRelations.AreEquivalent(destination.Type, expectedType) ||
            !TypeRelations.AreEquivalent(operand.Type, expectedType)
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                unary.Span,
                "IR unary instruction has incompatible operand or destination types."
            );
        }
    }

    private static void ValidateBinary(
        IrProgram program,
        IrInstruction.Binary binary,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, binary.Destination);
        IrSlot? left = GetSlot(program, binary.Left);
        IrSlot? right = GetSlot(program, binary.Right);

        if (destination is null || left is null || right is null)
        {
            ValidateSlot(program, binary.Destination, binary.Span, diagnostics);
            ValidateSlot(program, binary.Left, binary.Span, diagnostics);
            ValidateSlot(program, binary.Right, binary.Span, diagnostics);
            return;
        }

        bool isValid = binary.Operator switch
        {
            IrBinaryOperator.Add =>
                AreEquivalent(left, right, destination) &&
                destination.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number or
                    TypeKind.String,
            IrBinaryOperator.Subtract or
                IrBinaryOperator.Multiply or
                IrBinaryOperator.Divide or
                IrBinaryOperator.Remainder =>
                AreEquivalent(left, right, destination) &&
                destination.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number,
            IrBinaryOperator.LeftShift or
                IrBinaryOperator.RightShift =>
                AreType(left, TypeSymbols.Int) &&
                AreType(right, TypeSymbols.Int) &&
                AreType(destination, TypeSymbols.Int),
            IrBinaryOperator.BitwiseAnd or
                IrBinaryOperator.BitwiseXor or
                IrBinaryOperator.BitwiseOr =>
                AreEquivalent(left, right, destination) &&
                destination.Type.Kind is TypeKind.Int or TypeKind.Bool,
            IrBinaryOperator.LessThan or
                IrBinaryOperator.LessThanOrEqual or
                IrBinaryOperator.GreaterThan or
                IrBinaryOperator.GreaterThanOrEqual =>
                AreType(destination, TypeSymbols.Bool) &&
                TypeRelations.AreEquivalent(left.Type, right.Type) &&
                left.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number or
                    TypeKind.String,
            IrBinaryOperator.StructuralEqual or
                IrBinaryOperator.StructuralNotEqual or
                IrBinaryOperator.IdentityEqual or
                IrBinaryOperator.IdentityNotEqual =>
                AreType(destination, TypeSymbols.Bool),
            _ => false,
        };

        if (!isValid)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                binary.Span,
                "IR binary instruction has incompatible operand or destination types."
            );
        }
    }

    private static void ValidatePropertyRead(
        IrProgram program,
        IrInstruction.GetProperty property,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, property.Destination);
        IrSlot? target = GetSlot(program, property.Target);

        if (destination is null || target is null)
        {
            ValidateSlot(program, property.Destination, property.Span, diagnostics);
            ValidateSlot(program, property.Target, property.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        TypeSymbol? expectedType = null;

        if (property.IsArrayLength && targetType is ArrayTypeSymbol)
        {
            expectedType =
                property.IsOptional && target.Type is NullableTypeSymbol
                    ? TypeSymbols.Nullable(TypeSymbols.Int)
                    : TypeSymbols.Int;
        }
        else if (
            targetType.Kind == TypeKind.ErrorValue &&
            TryGetErrorPropertyType(
                property.Name,
                out TypeSymbol? errorPropertyType,
                out bool isOptional
            )
        )
        {
            expectedType =
                property.IsOptional && isOptional
                    ? MakeNullable(errorPropertyType)
                    : errorPropertyType;
        }
        else if (targetType is ObjectTypeSymbol objectType)
        {
            if (objectType.TryGetProperty(property.Name, out ObjectPropertySymbol? symbol))
            {
                expectedType =
                    property.IsOptional &&
                    (target.Type is NullableTypeSymbol || symbol.IsOptional)
                        ? MakeNullable(symbol.Type)
                        : symbol.Type;
            }
            else if (objectType.IsOpen)
            {
                expectedType = TypeSymbols.Nullable(TypeSymbols.Unknown);
            }
        }
        else if (targetType.Kind == TypeKind.Object)
        {
            expectedType = TypeSymbols.Nullable(TypeSymbols.Unknown);
        }

        if (
            expectedType is null ||
            !TypeRelations.AreEquivalent(destination.Type, expectedType)
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                property.Span,
                "IR property read has incompatible target or destination types."
            );
        }
    }

    private static void ValidatePropertyWrite(
        IrProgram program,
        IrInstruction.SetProperty property,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? target = GetSlot(program, property.Target);
        IrSlot? value = GetSlot(program, property.Value);

        if (target is null || value is null)
        {
            ValidateSlot(program, property.Target, property.Span, diagnostics);
            ValidateSlot(program, property.Value, property.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        TypeSymbol? expectedType = null;

        if (
            targetType is ObjectTypeSymbol objectType &&
            objectType.TryGetProperty(property.Name, out ObjectPropertySymbol? symbol)
        )
        {
            if (!symbol.IsReadOnly)
            {
                expectedType = symbol.Type;
            }
            else if (targetType.Kind == TypeKind.ErrorValue)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    property.Span,
                    $"IR cannot remove read-only error property '{property.Name}'."
                );
            }
        }
        else if (
            targetType.Kind == TypeKind.Object ||
            targetType is ObjectTypeSymbol { IsOpen: true }
        )
        {
            expectedType = TypeSymbols.Nullable(TypeSymbols.Unknown);
        }

        if (
            expectedType is null ||
            !TypeRelations.AreEquivalent(value.Type, expectedType)
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                property.Span,
                "IR property write has incompatible target or value types."
            );
        }
    }

    private static void ValidateElementRead(
        IrProgram program,
        IrInstruction.GetElement element,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, element.Destination);
        IrSlot? target = GetSlot(program, element.Target);
        IrSlot? index = GetSlot(program, element.Index);

        if (destination is null || target is null || index is null)
        {
            ValidateSlot(program, element.Destination, element.Span, diagnostics);
            ValidateSlot(program, element.Target, element.Span, diagnostics);
            ValidateSlot(program, element.Index, element.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        bool isValid;

        if (element.IsObjectAccess)
        {
            isValid =
                targetType.Kind is
                    TypeKind.Object or
                    TypeKind.StructuredObject or
                    TypeKind.ErrorValue &&
                AreType(index, TypeSymbols.String);
        }
        else
        {
            isValid =
                targetType is ArrayTypeSymbol arrayType &&
                AreType(index, TypeSymbols.Int) &&
                TypeRelations.AreEquivalent(
                    destination.Type,
                    element.IsOptional && target.Type is NullableTypeSymbol
                        ? MakeNullable(arrayType.ElementType)
                        : arrayType.ElementType
                );
        }

        if (!isValid)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                element.Span,
                "IR element read has incompatible target, index, or destination types."
            );
        }
    }

    private static void ValidateElementWrite(
        IrProgram program,
        IrInstruction.SetElement element,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? target = GetSlot(program, element.Target);
        IrSlot? index = GetSlot(program, element.Index);
        IrSlot? value = GetSlot(program, element.Value);

        if (target is null || index is null || value is null)
        {
            ValidateSlot(program, element.Target, element.Span, diagnostics);
            ValidateSlot(program, element.Index, element.Span, diagnostics);
            ValidateSlot(program, element.Value, element.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        bool isValid = element.IsObjectAccess
            ? IsValidObjectElementWrite(program, targetType, index, value)
            : targetType is ArrayTypeSymbol { IsReadOnly: false } arrayType &&
            AreType(index, TypeSymbols.Int) &&
            TypeRelations.AreEquivalent(value.Type, arrayType.ElementType);

        if (!isValid)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                element.Span,
                "IR element write has incompatible target, index, or value types."
            );
        }

        static bool IsValidObjectElementWrite(
            IrProgram program,
            TypeSymbol targetType,
            IrSlot index,
            IrSlot value
        )
        {
            if (
                targetType.Kind is not
                    TypeKind.Object and not
                    TypeKind.StructuredObject ||
                !AreType(index, TypeSymbols.String)
            )
            {
                return false;
            }

            if (
                targetType is not ObjectTypeSymbol objectType ||
                !TryGetConstantString(
                    program,
                    index.Id,
                    out string propertyName
                )
            )
            {
                return true;
            }

            if (
                objectType.TryGetProperty(
                    propertyName,
                    out ObjectPropertySymbol? declaration
                )
            )
            {
                return !declaration.IsReadOnly &&
                    TypeRelations.AreEquivalent(declaration.Type, value.Type);
            }

            return objectType.IsOpen &&
                TypeRelations.AreEquivalent(
                    TypeSymbols.Nullable(TypeSymbols.Unknown),
                    value.Type
                );
        }
    }

    private static bool TryGetConstantString(
        IrProgram program,
        int slotId,
        out string value
    )
    {
        IReadOnlyList<IrInstruction.Constant> definitions =
        [
            .. program.Blocks
                .SelectMany(static block => block.Instructions)
                .OfType<IrInstruction.Constant>()
                .Where(constant => constant.Destination == slotId),
        ];

        if (
            definitions.Count == 1 &&
            definitions[0].Value is string stringValue
        )
        {
            value = stringValue;
            return true;
        }

        value = "";
        return false;
    }

    private static bool IsConstantCompatible(object? value, TypeSymbol type)
    {
        if (type is NullableTypeSymbol nullable)
        {
            return value is null || IsConstantCompatible(value, nullable.UnderlyingType);
        }

        return type.Kind switch
        {
            TypeKind.Bool => value is bool,
            TypeKind.Int => value is long,
            TypeKind.Float => value is double,
            TypeKind.Number => value is long or double,
            TypeKind.String => value is string,
            TypeKind.Primitive => value is bool or long or double or string,
            TypeKind.Unknown => value is not null,
            _ => false,
        };
    }

    private static bool AreEquivalent(
        IrSlot first,
        IrSlot second,
        IrSlot third
    )
    {
        return TypeRelations.AreEquivalent(first.Type, second.Type) &&
            TypeRelations.AreEquivalent(first.Type, third.Type);
    }

    private static bool AreType(IrSlot slot, TypeSymbol type)
    {
        return TypeRelations.AreEquivalent(slot.Type, type);
    }

    private static TypeSymbol GetNonNullable(TypeSymbol type)
    {
        return type is NullableTypeSymbol nullable
            ? nullable.UnderlyingType
            : type;
    }

    private static TypeSymbol MakeNullable(TypeSymbol type)
    {
        return type is NullableTypeSymbol
            ? type
            : TypeSymbols.Nullable(type);
    }

    private static bool TryGetErrorPropertyType(
        string name,
        out TypeSymbol type,
        out bool isOptional
    )
    {
        switch (name)
        {
            case "code":
            case "category":
            case "message":
            {
                type = TypeSymbols.String;
                isOptional = false;
                return true;
            }

            case "cause":
            {
                type = TypeSymbols.ErrorValue;
                isOptional = true;
                return true;
            }

            case "data":
            {
                type = TypeSymbols.Nullable(TypeSymbols.Unknown);
                isOptional = true;
                return true;
            }

            case "spanStart":
            case "spanLength":
            {
                type = TypeSymbols.Int;
                isOptional = false;
                return true;
            }

            default:
            {
                type = TypeSymbols.ErrorRecovery;
                isOptional = false;
                return false;
            }
        }
    }

    private static void ValidateProviderCall(
        IrProgram program,
        EnvironmentSchema environment,
        IrInstruction.ProviderCall call,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (!environment.TryGetFunctionById(call.FunctionId, out FunctionSymbol? function))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.UndefinedProviderSymbol,
                call.Span,
                $"Function '{call.FunctionId}' is not declared."
            );
            return;
        }

        ValidateCallShape(
            program,
            call.Destination,
            call.ReturnType,
            call.Arguments,
            function.Name,
            function.ReturnType,
            [ .. function.Parameters.Select(static parameter => parameter.Type) ],
            call.Span,
            diagnostics
        );
    }

    private static void ValidateUserCall(
        IrProgram program,
        IrInstruction.UserCall call,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrFunction? function = program.UserFunctions.FirstOrDefault(
            candidate => candidate.Id == call.FunctionId
        );

        if (function is null)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.UndefinedUserFunction,
                call.Span,
                $"User function '{call.FunctionId}' is not declared."
            );
            return;
        }

        IReadOnlyList<TypeSymbol> parameterTypes =
        [
            .. function.Slots
                .Where(static slot => slot.Kind == IrSlotKind.Parameter)
                .Select(static slot => slot.Type),
        ];

        ValidateCallShape(
            program,
            call.Destination,
            call.ReturnType,
            call.Arguments,
            function.Id,
            function.ReturnType,
            parameterTypes,
            call.Span,
            diagnostics
        );
    }

    private static void ValidateCallShape(
        IrProgram program,
        int? destination,
        TypeSymbol returnType,
        IReadOnlyList<int> arguments,
        string functionName,
        TypeSymbol expectedReturnType,
        IReadOnlyList<TypeSymbol> parameterTypes,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (
            returnType.Kind == TypeKind.Void &&
            destination is not null ||
            returnType.Kind != TypeKind.Void &&
            destination is null
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                "IR call destination does not match the function return type."
            );
        }

        if (!TypeRelations.AreEquivalent(returnType, expectedReturnType))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR call return type does not match function '{functionName}'."
            );
        }

        if (destination is not null)
        {
            ValidateSlotType(
                program,
                destination.Value,
                expectedReturnType,
                span,
                diagnostics
            );
        }

        if (arguments.Count != parameterTypes.Count)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR call to '{functionName}' has an invalid argument count."
            );
            return;
        }

        for (int index = 0; index < arguments.Count; index++)
        {
            ValidateSlotAssignable(
                program,
                arguments[index],
                parameterTypes[index],
                span,
                diagnostics
            );
        }
    }

    private static void ValidateSlotAssignable(
        IrProgram program,
        int slotId,
        TypeSymbol expectedType,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? slot = GetSlot(program, slotId);

        if (slot is null)
        {
            ValidateSlot(program, slotId, span, diagnostics);
            return;
        }

        if (!TypeRelations.IsAssignable(slot.Type, expectedType))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR slot {slotId} has type '{slot.Type.DisplayName}', which is not assignable to '{expectedType.DisplayName}'."
            );
        }
    }

    private static void ValidateDefinitions(
        IrProgram program,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (!blocksById.ContainsKey(program.EntryBlock))
        {
            return;
        }

        IReadOnlyCollection<int> reachable = GetReachableBlocks(program.EntryBlock, blocksById);
        IReadOnlyDictionary<int, IReadOnlyList<int>> predecessors = GetPredecessors(reachable, blocksById);
        IReadOnlySet<int> allSlots = new HashSet<int>(program.Slots.Select(static slot => slot.Id));
        IReadOnlySet<int> parameterSlots = new HashSet<int>(
            program.Slots
                .Where(static slot => slot.Kind == IrSlotKind.Parameter)
                .Select(static slot => slot.Id)
        );
        Dictionary<int, IReadOnlySet<int>> outgoing = [ ];

        foreach (int blockId in reachable)
        {
            IReadOnlySet<int> initialDefinitions =
                blockId == program.EntryBlock
                    ? parameterSlots
                    : new HashSet<int>(allSlots);
            outgoing.Add(
                blockId,
                initialDefinitions
            );
        }

        bool changed;

        do
        {
            changed = false;

            foreach (int blockId in reachable.Order())
            {
                IReadOnlySet<int> incoming = GetIncomingDefinitions(
                    program,
                    blockId,
                    program.EntryBlock,
                    predecessors,
                    outgoing,
                    parameterSlots,
                    blocksById,
                    lifetimeRegionsById
                );
                IReadOnlySet<int> definitions = ApplyDefinitions(
                    blocksById[blockId],
                    incoming
                );

                if (!outgoing[blockId].SetEquals(definitions))
                {
                    outgoing[blockId] = definitions;
                    changed = true;
                }
            }
        }
        while (changed);

        foreach (int blockId in reachable)
        {
            IReadOnlySet<int> defined = GetIncomingDefinitions(
                program,
                blockId,
                program.EntryBlock,
                predecessors,
                outgoing,
                parameterSlots,
                blocksById,
                lifetimeRegionsById
            );
            ValidateUses(blocksById[blockId], defined, diagnostics);
        }

        foreach (IrExceptionRegion region in program.EntryFunction.ExceptionRegions)
        {
            IReadOnlySet<int> protectedDefinitions = reachable.Contains(
                region.Protected.EntryBlock
            )
                ? GetIncomingDefinitions(
                    program,
                    region.Protected.EntryBlock,
                    program.EntryBlock,
                    predecessors,
                    outgoing,
                    parameterSlots,
                    blocksById,
                    lifetimeRegionsById
                )
                : parameterSlots;

            if (region.Handler is IrExceptionHandler handler)
            {
                ValidateComponentDefinitions(
                    handler.EntryBlock,
                    handler.Blocks,
                    protectedDefinitions.Append(handler.ErrorSlot)
                );
            }

            if (region.Cleanup is IrExceptionCleanup cleanup)
            {
                ValidateComponentDefinitions(
                    cleanup.EntryBlock,
                    cleanup.Blocks,
                    protectedDefinitions
                );
            }
        }

        void ValidateComponentDefinitions(
            int componentEntry,
            IReadOnlyCollection<int> componentBlocks,
            IEnumerable<int> initialDefinitions
        )
        {
            IReadOnlySet<int> allowedBlocks = componentBlocks.ToHashSet();
            IReadOnlyCollection<int> componentReachable = GetReachableBlocks(
                componentEntry,
                blocksById
            ).Where(allowedBlocks.Contains).ToArray();
            IReadOnlyDictionary<int, IReadOnlyList<int>> componentPredecessors =
                GetPredecessors(componentReachable, blocksById);
            IReadOnlySet<int> entryDefinitions = new HashSet<int>(
                initialDefinitions
            );
            Dictionary<int, IReadOnlySet<int>> componentOutgoing =
                componentReachable.ToDictionary(
                    static blockId => blockId,
                    _ => allSlots
                );
            bool componentChanged;

            do
            {
                componentChanged = false;

                foreach (int blockId in componentReachable.Order())
                {
                    IReadOnlySet<int> incoming = GetIncomingDefinitions(
                        program,
                        blockId,
                        componentEntry,
                        componentPredecessors,
                        componentOutgoing,
                        entryDefinitions,
                        blocksById,
                        lifetimeRegionsById
                    );
                    IReadOnlySet<int> definitions = ApplyDefinitions(
                        blocksById[blockId],
                        incoming
                    );

                    if (!componentOutgoing[blockId].SetEquals(definitions))
                    {
                        componentOutgoing[blockId] = definitions;
                        componentChanged = true;
                    }
                }
            }
            while (componentChanged);

            foreach (int blockId in componentReachable)
            {
                IReadOnlySet<int> defined = GetIncomingDefinitions(
                    program,
                    blockId,
                    componentEntry,
                    componentPredecessors,
                    componentOutgoing,
                    entryDefinitions,
                    blocksById,
                    lifetimeRegionsById
                );
                ValidateUses(blocksById[blockId], defined, diagnostics);
            }
        }
    }

    private static IReadOnlySet<int> GetReachableBlocks(
        int entryBlock,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById
    )
    {
        HashSet<int> reachable = [ ];
        Queue<int> pending = new ();
        pending.Enqueue(entryBlock);

        while (pending.Count > 0)
        {
            int blockId = pending.Dequeue();

            if (
                reachable.Contains(blockId) ||
                !blocksById.TryGetValue(blockId, out IrBasicBlock? block)
            )
            {
                continue;
            }

            reachable.Add(blockId);

            foreach (int successor in GetSuccessors(block.Terminator))
            {
                pending.Enqueue(successor);
            }
        }

        return reachable;
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<int>> GetPredecessors(
        IReadOnlyCollection<int> reachable,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById
    )
    {
        Dictionary<int, IReadOnlyList<int>> predecessors = reachable.ToDictionary(
            static blockId => blockId,
            static IReadOnlyList<int> (_) => new List<int>()
        );

        foreach (int blockId in reachable)
        {
            foreach (int successor in GetSuccessors(blocksById[blockId].Terminator))
            {
                if (predecessors.TryGetValue(successor, out IReadOnlyList<int>? values))
                {
                    ((IList<int>)values).Add(blockId);
                }
            }
        }

        return predecessors;
    }

    private static IReadOnlySet<int> GetIncomingDefinitions(
        IrProgram program,
        int blockId,
        int entryBlock,
        IReadOnlyDictionary<int, IReadOnlyList<int>> predecessors,
        IReadOnlyDictionary<int, IReadOnlySet<int>> outgoing,
        IReadOnlySet<int> entryDefinitions,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById
    )
    {
        if (blockId == entryBlock)
        {
            return entryDefinitions;
        }

        if (predecessors[blockId].Count == 0)
        {
            return ReadOnlySet<int>.Empty;
        }

        int targetRegion = blocksById[blockId].LifetimeRegion;
        HashSet<int> incoming =
        [
            .. FilterDefinitionsForRegion(
                program,
                outgoing[predecessors[blockId][0]],
                targetRegion,
                lifetimeRegionsById
            ),
        ];

        foreach (int predecessor in predecessors[blockId].Skip(1))
        {
            incoming.IntersectWith(
                FilterDefinitionsForRegion(
                    program,
                    outgoing[predecessor],
                    targetRegion,
                    lifetimeRegionsById
                )
            );
        }

        return incoming;
    }

    private static IReadOnlySet<int> FilterDefinitionsForRegion(
        IrProgram program,
        IEnumerable<int> definitions,
        int targetRegion,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById
    )
    {
        return new HashSet<int>(
            definitions.Where(
                slotId =>
                {
                    IrSlot? slot = GetSlot(program, slotId);

                    return
                        slot is not null &&
                        IsRegionAncestor(
                            slot.LifetimeRegion,
                            targetRegion,
                            lifetimeRegionsById
                        );
                }
            )
        );
    }

    private static bool IsRegionAncestor(
        int possibleAncestor,
        int region,
        IReadOnlyDictionary<int, IrLifetimeRegion> lifetimeRegionsById
    )
    {
        int? current = region;

        while (
            current is int currentId &&
            lifetimeRegionsById.TryGetValue(
                currentId,
                out IrLifetimeRegion? currentRegion
            )
        )
        {
            if (currentId == possibleAncestor)
            {
                return true;
            }

            current = currentRegion.ParentRegion;
        }

        return false;
    }

    private static IReadOnlySet<int> ApplyDefinitions(
        IrBasicBlock block,
        IEnumerable<int> incoming
    )
    {
        HashSet<int> defined = [ .. incoming ];

        foreach (IrInstruction instruction in block.Instructions)
        {
            int? destination = GetDestination(instruction);

            if (destination is not null)
            {
                defined.Add(destination.Value);
            }
        }

        return defined;
    }

    private static void ValidateUses(
        IrBasicBlock block,
        IEnumerable<int> defined,
        ICollection<Diagnostic> diagnostics
    )
    {
        ISet<int> allDefined = new HashSet<int>(defined);

        foreach (IrInstruction instruction in block.Instructions)
        {
            foreach (int operand in GetOperands(instruction))
            {
                if (!allDefined.Contains(operand))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.UseBeforeDefinition,
                        instruction.Span,
                        $"IR slot {operand} is used before it is defined."
                    );
                }
            }

            int? destination = GetDestination(instruction);

            if (destination is not null)
            {
                allDefined.Add(destination.Value);
            }
        }

        foreach (int operand in GetOperands(block.Terminator))
        {
            if (!allDefined.Contains(operand))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.UseBeforeDefinition,
                    block.Terminator.Span,
                    $"IR slot {operand} is used before it is defined."
                );
            }
        }
    }

    private static int? GetDestination(IrInstruction instruction)
    {
        return instruction switch
        {
            IrInstruction.Constant value => value.Destination,
            IrInstruction.Copy copy => copy.Destination,
            IrInstruction.LoadGlobal global => global.Destination,
            IrInstruction.Unary unary => unary.Destination,
            IrInstruction.Binary binary => binary.Destination,
            IrInstruction.Convert conversion => conversion.Destination,
            IrInstruction.Truthiness truthiness => truthiness.Destination,
            IrInstruction.TypeTest typeTest => typeTest.Destination,
            IrInstruction.IsNull isNull => isNull.Destination,
            IrInstruction.HasProperty propertyTest => propertyTest.Destination,
            IrInstruction.CreateArray array => array.Destination,
            IrInstruction.CreateObject objectValue => objectValue.Destination,
            IrInstruction.GetProperty property => property.Destination,
            IrInstruction.GetElement element => element.Destination,
            IrInstruction.ProviderCall call => call.Destination,
            IrInstruction.UserCall call => call.Destination,
            _ => null,
        };
    }

    private static IEnumerable<int> GetOperands(IrInstruction instruction)
    {
        return instruction switch
        {
            IrInstruction.Copy copy => [ copy.Source ],
            IrInstruction.Unary unary => [ unary.Operand ],
            IrInstruction.Binary binary => [ binary.Left, binary.Right ],
            IrInstruction.Convert conversion => [ conversion.Source ],
            IrInstruction.Truthiness truthiness => [ truthiness.Source ],
            IrInstruction.TypeTest typeTest => [ typeTest.Source ],
            IrInstruction.IsNull isNull => [ isNull.Source ],
            IrInstruction.HasProperty propertyTest =>
                [ propertyTest.Target, propertyTest.Key ],
            IrInstruction.CreateArray array => array.Elements,
            IrInstruction.CreateObject objectValue =>
                objectValue.Properties.Select(static property => property.Value),
            IrInstruction.GetProperty property => [ property.Target ],
            IrInstruction.SetProperty property => [ property.Target, property.Value ],
            IrInstruction.RemoveProperty property => [ property.Target ],
            IrInstruction.GetElement element => [ element.Target, element.Index ],
            IrInstruction.SetElement element =>
                [ element.Target, element.Index, element.Value ],
            IrInstruction.RemoveElementProperty property =>
                [ property.Target, property.Key ],
            IrInstruction.ProviderCall call => call.Arguments,
            IrInstruction.UserCall call => call.Arguments,
            _ => [ ],
        };
    }

    private static IEnumerable<int> GetOperands(IrTerminator terminator)
    {
        return terminator switch
        {
            IrTerminator.Branch branch => [ branch.Condition ],
            IrTerminator.Return { Value: { } value } => [ value ],
            IrTerminator.Throw error => [ error.Error ],
            _ => [ ],
        };
    }

    private static IEnumerable<int> GetSuccessors(IrTerminator terminator)
    {
        return terminator switch
        {
            IrTerminator.Jump jump => [ jump.TargetBlock ],
            IrTerminator.Branch branch => [ branch.TrueBlock, branch.FalseBlock ],
            _ => [ ],
        };
    }

    private static void ValidateEquivalentSlots(
        IrProgram program,
        int destination,
        int source,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destinationSlot = GetSlot(program, destination);
        IrSlot? sourceSlot = GetSlot(program, source);

        if (destinationSlot is null || sourceSlot is null)
        {
            ValidateSlot(program, destination, span, diagnostics);
            ValidateSlot(program, source, span, diagnostics);
            return;
        }

        if (
            !TypeRelations.IsAssignable(sourceSlot.Type, destinationSlot.Type) ||
            !TypeRelations.IsCastable(sourceSlot.Type, destinationSlot.Type)
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR cannot copy '{sourceSlot.Type.DisplayName}' into '{destinationSlot.Type.DisplayName}'."
            );
        }
    }

    private static void ValidateSlotType(
        IrProgram program,
        int slotId,
        TypeSymbol expectedType,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? slot = GetSlot(program, slotId);

        if (slot is null)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidSlot,
                span,
                $"IR slot {slotId} does not exist."
            );
            return;
        }

        if (!TypeRelations.AreEquivalent(slot.Type, expectedType))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR slot {slotId} has type '{slot.Type.DisplayName}', expected '{expectedType.DisplayName}'."
            );
        }
    }

    private static void ValidateSlotViewType(
        IrProgram program,
        int slotId,
        TypeSymbol expectedType,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? slot = GetSlot(program, slotId);

        if (slot is null)
        {
            ValidateSlot(program, slotId, span, diagnostics);
            return;
        }

        if (!TypeRelations.IsViewCompatible(slot.Type, expectedType))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR slot {slotId} has type '{slot.Type.DisplayName}', but a read-compatible '{expectedType.DisplayName}' value is required."
            );
        }
    }

    private static void ValidateSlot(
        IrProgram program,
        int slotId,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (GetSlot(program, slotId) is null)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidSlot,
                span,
                $"IR slot {slotId} does not exist."
            );
        }
    }

    private static IrSlot? GetSlot(IrProgram program, int slotId)
    {
        return slotId >= 0 && slotId < program.Slots.Count
            ? program.Slots[slotId]
            : null;
    }

    private static void ValidateBlockTarget(
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        int target,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (!blocksById.ContainsKey(target))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidStructure,
                span,
                $"IR block target {target} does not exist."
            );
        }
    }

    private static void Report(
        ICollection<Diagnostic> diagnostics,
        string code,
        TextSpan span,
        string message
    )
    {
        diagnostics.Add(
            new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticCategory.Exporter,
                span,
                message
            )
        );
    }

    private sealed record ExceptionBlockOwner(
        int RegionId,
        IrExceptionRegionPart Part
    );
}
