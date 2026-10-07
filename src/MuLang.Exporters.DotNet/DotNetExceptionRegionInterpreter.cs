using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.IR;

namespace MuLang.Exporters.DotNet;

internal sealed class DotNetExceptionRegionInterpreter
{
    private readonly IrFunction function;
    private readonly EnvironmentFingerprint environmentFingerprint;
    private readonly bool validateEnvironment;
    private readonly IReadOnlyDictionary<int, BlockOwner> blockOwners;

    public DotNetExceptionRegionInterpreter(
        IrFunction function,
        EnvironmentFingerprint environmentFingerprint,
        bool validateEnvironment
    )
    {
        this.function = function;
        this.environmentFingerprint = environmentFingerprint;
        this.validateEnvironment = validateEnvironment;
        blockOwners = CreateBlockOwners(function);
    }

    public DotNetUserFunction Compile()
    {
        return Execute;
    }

    private object? Execute(
        DotNetRuntimeContext context,
        DotNetUserFunctionExecution execution,
        object?[] arguments
    )
    {
        object?[] slots = new object?[function.Slots.Count];
        int parameterIndex = 0;

        foreach (IrSlot slot in function.Slots)
        {
            if (slot.Kind != IrSlotKind.Parameter)
            {
                break;
            }

            slots[slot.Id] = arguments[parameterIndex++];
        }

        int blockId = function.EntryBlock;
        PendingCompletion? pending = null;
        TextSpan entrySpan = function.Blocks[function.EntryBlock].Terminator.Span;

        if (validateEnvironment)
        {
            DotNetRuntimeOperations.ValidateEnvironment(
                context,
                environmentFingerprint,
                entrySpan
            );
        }

        while (true)
        {
            IrBasicBlock block = function.Blocks[blockId];

            try
            {
                foreach (IrInstruction instruction in block.Instructions)
                {
                    DotNetRuntimeOperations.Consume(context, instruction.Span);
                    ExecuteInstruction(
                        context,
                        execution,
                        slots,
                        instruction
                    );
                }

                DotNetRuntimeOperations.Consume(context, block.Terminator.Span);

                switch (block.Terminator)
                {
                    case IrTerminator.Jump jump:
                    {
                        if (TryDispatch(
                                block.Id,
                                PendingCompletion.Jump(jump.TargetBlock),
                                slots,
                                ref blockId,
                                ref pending
                            ))
                        {
                            continue;
                        }

                        blockId = jump.TargetBlock;
                        continue;
                    }

                    case IrTerminator.Branch branch:
                    {
                        int target = DotNetRuntimeOperations.RequireBoolean(
                            slots[branch.Condition],
                            branch.Span
                        )
                            ? branch.TrueBlock
                            : branch.FalseBlock;

                        if (TryDispatch(
                                block.Id,
                                PendingCompletion.Jump(target),
                                slots,
                                ref blockId,
                                ref pending
                            ))
                        {
                            continue;
                        }

                        blockId = target;
                        continue;
                    }

                    case IrTerminator.Return result:
                    {
                        PendingCompletion completion = PendingCompletion.Return(
                            result.Value is int valueSlot
                                ? slots[valueSlot]
                                : null
                        );

                        if (TryDispatch(
                                block.Id,
                                completion,
                                slots,
                                ref blockId,
                                ref pending
                            ))
                        {
                            continue;
                        }

                        return completion.Value;
                    }

                    case IrTerminator.Throw error:
                    {
                        IDotNetErrorValue errorValue =
                            DotNetRuntimeOperations.RequireErrorValue(
                                context,
                                slots[error.Error],
                                error.Span
                            );

                        if (!errorValue.Error.IsCatchable)
                        {
                            throw new MuLangRuntimeException(errorValue.Error);
                        }

                        PendingCompletion completion =
                            CreateErrorCompletion(block.Id, errorValue);

                        if (TryDispatch(
                                block.Id,
                                completion,
                                slots,
                                ref blockId,
                                ref pending
                            ))
                        {
                            continue;
                        }

                        throw new MuLangRuntimeException(errorValue.Error);
                    }

                    case IrTerminator.Resume:
                    {
                        if (pending is null)
                        {
                            throw new InvalidOperationException(
                                "IR cleanup resumed without a pending completion."
                            );
                        }

                        PendingCompletion completion = pending;
                        pending = null;

                        if (ContinuePending(
                                completion,
                                slots,
                                ref blockId,
                                ref pending,
                                out object? returnValue
                            ))
                        {
                            continue;
                        }

                        return returnValue;
                    }

                    default:
                    {
                        throw new InvalidOperationException(
                            "Unknown IR terminator."
                        );
                    }
                }
            }
            catch (MuLangRuntimeException exception)
            {
                if (!exception.Error.IsCatchable)
                {
                    throw;
                }

                pending = null;
                PendingCompletion completion = CreateErrorCompletion(
                    block.Id,
                    new DotNetErrorValue(exception.Error)
                );

                if (TryDispatch(
                        block.Id,
                        completion,
                        slots,
                        ref blockId,
                        ref pending
                    ))
                {
                    continue;
                }

                throw;
            }
        }
    }

    private void ExecuteInstruction(
        DotNetRuntimeContext context,
        DotNetUserFunctionExecution execution,
        object?[] slots,
        IrInstruction instruction
    )
    {
        switch (instruction)
        {
            case IrInstruction.Constant value:
            {
                slots[value.Destination] = value.Value;
                break;
            }

            case IrInstruction.Copy copy:
            {
                slots[copy.Destination] = slots[copy.Source];
                break;
            }

            case IrInstruction.LoadGlobal global:
            {
                slots[global.Destination] = DotNetRuntimeOperations.GetGlobal(
                    context,
                    global.GlobalId,
                    function.Slots[global.Destination].Type,
                    global.Span
                );
                break;
            }

            case IrInstruction.Unary unary:
            {
                slots[unary.Destination] = DotNetRuntimeOperations.Unary(
                    unary.Operator,
                    slots[unary.Operand],
                    unary.Span
                );
                break;
            }

            case IrInstruction.Binary binary:
            {
                slots[binary.Destination] = DotNetRuntimeOperations.Binary(
                    context,
                    binary.Operator,
                    slots[binary.Left],
                    slots[binary.Right],
                    binary.Span
                );
                break;
            }

            case IrInstruction.Convert conversion:
            {
                slots[conversion.Destination] =
                    DotNetRuntimeOperations.ConvertValue(
                        context,
                        slots[conversion.Source],
                        conversion.TargetType,
                        conversion.Kind,
                        conversion.Span
                    );
                break;
            }

            case IrInstruction.Truthiness truthiness:
            {
                slots[truthiness.Destination] =
                    DotNetRuntimeOperations.Truthiness(
                        slots[truthiness.Source],
                        truthiness.Span
                    );
                break;
            }

            case IrInstruction.TypeTest typeTest:
            {
                slots[typeTest.Destination] = DotNetRuntimeOperations.TypeTest(
                    context,
                    slots[typeTest.Source],
                    typeTest.TestedType,
                    typeTest.Span
                );
                break;
            }

            case IrInstruction.IsNull isNull:
            {
                slots[isNull.Destination] =
                    DotNetRuntimeOperations.IsNull(slots[isNull.Source]);
                break;
            }

            case IrInstruction.HasProperty property:
            {
                slots[property.Destination] =
                    DotNetRuntimeOperations.HasProperty(
                        slots[property.Target],
                        slots[property.Key],
                        property.Span
                    );
                break;
            }

            case IrInstruction.CreateArray array:
            {
                slots[array.Destination] = DotNetRuntimeOperations.CreateArray(
                    array.Elements.Select(element => slots[element]),
                    array.Type.IsReadOnly
                );
                break;
            }

            case IrInstruction.CreateObject objectValue:
            {
                slots[objectValue.Destination] =
                    DotNetRuntimeOperations.CreateObject(
                        objectValue.Type,
                        objectValue.Properties.Select(
                            static property => property.Name
                        ),
                        objectValue.Properties.Select(
                            property => slots[property.Value]
                        )
                    );
                break;
            }

            case IrInstruction.GetProperty property:
            {
                slots[property.Destination] =
                    DotNetRuntimeOperations.GetProperty(
                        slots[property.Target],
                        property.Name,
                        property.IsOptional,
                        property.IsArrayLength,
                        function.Slots[property.Destination].Type,
                        property.Span
                    );
                break;
            }

            case IrInstruction.SetProperty property:
            {
                DotNetRuntimeOperations.SetProperty(
                    slots[property.Target],
                    property.Name,
                    slots[property.Value],
                    property.Span
                );
                break;
            }

            case IrInstruction.RemoveProperty property:
            {
                DotNetRuntimeOperations.RemoveProperty(
                    slots[property.Target],
                    property.Name,
                    property.Span
                );
                break;
            }

            case IrInstruction.GetElement element:
            {
                slots[element.Destination] = DotNetRuntimeOperations.GetElement(
                    context,
                    slots[element.Target],
                    slots[element.Index],
                    element.IsObjectAccess,
                    element.IsOptional,
                    function.Slots[element.Destination].Type,
                    element.Span
                );
                break;
            }

            case IrInstruction.SetElement element:
            {
                DotNetRuntimeOperations.SetElement(
                    slots[element.Target],
                    slots[element.Index],
                    slots[element.Value],
                    element.IsObjectAccess,
                    element.Span
                );
                break;
            }

            case IrInstruction.RemoveElementProperty property:
            {
                DotNetRuntimeOperations.RemoveElementProperty(
                    slots[property.Target],
                    slots[property.Key],
                    property.Span
                );
                break;
            }

            case IrInstruction.ProviderCall call:
            {
                object? result = DotNetRuntimeOperations.Invoke(
                    context,
                    call.FunctionId,
                    call.Arguments.Select(argument => slots[argument]).ToArray(),
                    call.ReturnType,
                    call.Span
                );

                if (call.Destination is int destination)
                {
                    slots[destination] = result;
                }

                break;
            }

            case IrInstruction.UserCall call:
            {
                object? result = execution.Invoke(
                    context,
                    call.FunctionId,
                    call.Arguments.Select(argument => slots[argument]).ToArray(),
                    call.Span
                );

                if (call.Destination is int destination)
                {
                    slots[destination] = result;
                }

                break;
            }

            default:
            {
                throw new InvalidOperationException("Unknown IR instruction.");
            }
        }
    }

    private bool TryDispatch(
        int sourceBlock,
        PendingCompletion completion,
        object?[] slots,
        ref int blockId,
        ref PendingCompletion? pending
    )
    {
        IReadOnlyList<int> cleanupEntries = GetCleanupEntries(
            sourceBlock,
            completion.TargetBlock
        );

        if (cleanupEntries.Count == 0)
        {
            if (completion.Kind != CompletionKind.Handler)
            {
                return false;
            }

            return ContinuePending(
                completion,
                slots,
                ref blockId,
                ref pending,
                out _
            );
        }

        completion = completion with
        {
            CleanupEntries = cleanupEntries,
            CleanupIndex = 1,
        };
        pending = completion;
        blockId = cleanupEntries[0];
        return true;
    }

    private bool ContinuePending(
        PendingCompletion completion,
        object?[] slots,
        ref int blockId,
        ref PendingCompletion? pending,
        out object? returnValue
    )
    {
        if (completion.CleanupIndex < completion.CleanupEntries.Count)
        {
            blockId = completion.CleanupEntries[completion.CleanupIndex];
            pending = completion with
            {
                CleanupIndex = completion.CleanupIndex + 1,
            };
            returnValue = null;
            return true;
        }

        switch (completion.Kind)
        {
            case CompletionKind.Jump:
            {
                blockId = completion.TargetBlock ??
                    throw new InvalidOperationException("Jump target is missing.");
                returnValue = null;
                return true;
            }

            case CompletionKind.Handler:
            {
                IrExceptionHandler handler = function.ExceptionRegions[
                    completion.HandlerRegion ??
                    throw new InvalidOperationException("Handler region is missing.")
                ].Handler ?? throw new InvalidOperationException("Handler is missing.");
                slots[handler.ErrorSlot] = completion.Error;
                blockId = handler.EntryBlock;
                returnValue = null;
                return true;
            }

            case CompletionKind.Return:
            {
                returnValue = completion.Value;
                return false;
            }

            case CompletionKind.Error:
            {
                throw new MuLangRuntimeException(
                    completion.Error?.Error ??
                    throw new InvalidOperationException("Pending error is missing.")
                );
            }

            default:
            {
                throw new InvalidOperationException("Pending completion is invalid.");
            }
        }
    }

    private PendingCompletion CreateErrorCompletion(
        int sourceBlock,
        IDotNetErrorValue error
    )
    {
        int? handlerRegion = null;

        foreach (BlockOwner membership in GetMembership(sourceBlock))
        {
            if (
                membership.Part == IrExceptionRegionPart.Protected &&
                function.ExceptionRegions[membership.RegionId].Handler is not null
            )
            {
                handlerRegion = membership.RegionId;
                break;
            }
        }

        return handlerRegion is int regionId
            ? PendingCompletion.Handler(
                function.ExceptionRegions[regionId].Handler!.EntryBlock,
                regionId,
                error
            )
            : PendingCompletion.Propagate(error);
    }

    private IReadOnlyList<int> GetCleanupEntries(
        int sourceBlock,
        int? targetBlock
    )
    {
        IReadOnlyList<BlockOwner> sourceMembership = GetMembership(sourceBlock);
        IReadOnlySet<int> targetRegions = targetBlock is int target
            ? GetMembership(target)
                .Select(static membership => membership.RegionId)
                .ToHashSet()
            : new HashSet<int>();
        IList<int> cleanups = [ ];

        foreach (BlockOwner membership in sourceMembership)
        {
            if (targetRegions.Contains(membership.RegionId))
            {
                break;
            }

            IrExceptionRegion region = function.ExceptionRegions[
                membership.RegionId
            ];

            if (
                membership.Part != IrExceptionRegionPart.Cleanup &&
                region.Cleanup is IrExceptionCleanup cleanup
            )
            {
                cleanups.Add(cleanup.EntryBlock);
            }
        }

        return cleanups.AsReadOnly();
    }

    private IReadOnlyList<BlockOwner> GetMembership(int blockId)
    {
        if (!blockOwners.TryGetValue(blockId, out BlockOwner? owner))
        {
            return [ ];
        }

        IList<BlockOwner> membership = [ owner ];
        IrExceptionRegion region = function.ExceptionRegions[owner.RegionId];

        while (
            region.ParentRegion is int parentRegion &&
            region.ParentPart is IrExceptionRegionPart parentPart
        )
        {
            membership.Add(new BlockOwner(parentRegion, parentPart));
            region = function.ExceptionRegions[parentRegion];
        }

        return membership.AsReadOnly();
    }

    private static IReadOnlyDictionary<int, BlockOwner> CreateBlockOwners(
        IrFunction function
    )
    {
        Dictionary<int, BlockOwner> owners = [ ];

        foreach (IrExceptionRegion region in function.ExceptionRegions)
        {
            Add(region.Protected.Blocks, IrExceptionRegionPart.Protected);

            if (region.Handler is IrExceptionHandler handler)
            {
                Add(handler.Blocks, IrExceptionRegionPart.Handler);
            }

            if (region.Cleanup is IrExceptionCleanup cleanup)
            {
                Add(cleanup.Blocks, IrExceptionRegionPart.Cleanup);
            }

            void Add(
                IEnumerable<int> blocks,
                IrExceptionRegionPart part
            )
            {
                foreach (int block in blocks)
                {
                    owners.Add(block, new BlockOwner(region.Id, part));
                }
            }
        }

        return owners;
    }

    private enum CompletionKind
    {
        Jump,
        Handler,
        Return,
        Error,
    }

    private sealed record BlockOwner(
        int RegionId,
        IrExceptionRegionPart Part
    );

    private sealed record PendingCompletion(
        CompletionKind Kind,
        int? TargetBlock,
        int? HandlerRegion,
        object? Value,
        IDotNetErrorValue? Error,
        IReadOnlyList<int> CleanupEntries,
        int CleanupIndex
    )
    {
        public static PendingCompletion Jump(int targetBlock) =>
            new (
                CompletionKind.Jump,
                targetBlock,
                null,
                null,
                null,
                [ ],
                0
            );

        public static PendingCompletion Handler(
            int targetBlock,
            int handlerRegion,
            IDotNetErrorValue error
        ) =>
            new (
                CompletionKind.Handler,
                targetBlock,
                handlerRegion,
                null,
                error,
                [ ],
                0
            );

        public static PendingCompletion Return(object? value) =>
            new (
                CompletionKind.Return,
                null,
                null,
                value,
                null,
                [ ],
                0
            );

        public static PendingCompletion Propagate(IDotNetErrorValue error) =>
            new (
                CompletionKind.Error,
                null,
                null,
                null,
                error,
                [ ],
                0
            );
    }
}
