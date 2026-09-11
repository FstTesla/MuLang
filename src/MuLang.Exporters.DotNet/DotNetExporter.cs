using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;
using System.Linq.Expressions;
using System.Reflection;

namespace MuLang.Exporters.DotNet;

internal static class DotNetExporter
{
    private static readonly MethodInfo validateEnvironmentMethod = GetMethod(
        nameof(DotNetRuntimeOperations.ValidateEnvironment),
        typeof(DotNetRuntimeContext),
        typeof(EnvironmentFingerprint),
        typeof(TextSpan)
    );

    private static readonly MethodInfo consumeMethod = GetMethod(
        nameof(DotNetRuntimeOperations.Consume),
        typeof(DotNetRuntimeContext),
        typeof(TextSpan)
    );

    private static readonly MethodInfo getGlobalMethod = GetMethod(
        nameof(DotNetRuntimeOperations.GetGlobal),
        typeof(DotNetRuntimeContext),
        typeof(string),
        typeof(TypeSymbol),
        typeof(TextSpan)
    );

    private static readonly MethodInfo invokeMethod = GetMethod(
        nameof(DotNetRuntimeOperations.Invoke),
        typeof(DotNetRuntimeContext),
        typeof(string),
        typeof(object[]),
        typeof(TypeSymbol),
        typeof(TextSpan)
    );

    private static readonly MethodInfo requireBooleanMethod = GetMethod(
        nameof(DotNetRuntimeOperations.RequireBoolean),
        typeof(object),
        typeof(TextSpan)
    );

    private static readonly MethodInfo unaryMethod = GetMethod(
        nameof(DotNetRuntimeOperations.Unary),
        typeof(IrUnaryOperator),
        typeof(object),
        typeof(TextSpan)
    );

    private static readonly MethodInfo binaryMethod = GetMethod(
        nameof(DotNetRuntimeOperations.Binary),
        typeof(DotNetRuntimeContext),
        typeof(IrBinaryOperator),
        typeof(object),
        typeof(object),
        typeof(TextSpan)
    );

    private static readonly MethodInfo convertMethod = GetMethod(
        nameof(DotNetRuntimeOperations.ConvertValue),
        typeof(DotNetRuntimeContext),
        typeof(object),
        typeof(TypeSymbol),
        typeof(TextSpan)
    );

    private static readonly MethodInfo typeTestMethod = GetMethod(
        nameof(DotNetRuntimeOperations.TypeTest),
        typeof(DotNetRuntimeContext),
        typeof(object),
        typeof(TypeSymbol),
        typeof(TextSpan)
    );

    private static readonly MethodInfo isNullMethod = GetMethod(
        nameof(DotNetRuntimeOperations.IsNull),
        typeof(object)
    );

    private static readonly MethodInfo createArrayMethod = GetMethod(
        nameof(DotNetRuntimeOperations.CreateArray),
        typeof(object[])
    );

    private static readonly MethodInfo createObjectMethod = GetMethod(
        nameof(DotNetRuntimeOperations.CreateObject),
        typeof(string[]),
        typeof(object[])
    );

    private static readonly MethodInfo getPropertyMethod = GetMethod(
        nameof(DotNetRuntimeOperations.GetProperty),
        typeof(object),
        typeof(string),
        typeof(bool),
        typeof(bool),
        typeof(TypeSymbol),
        typeof(TextSpan)
    );

    private static readonly MethodInfo setPropertyMethod = GetMethod(
        nameof(DotNetRuntimeOperations.SetProperty),
        typeof(object),
        typeof(string),
        typeof(object),
        typeof(TextSpan)
    );

    private static readonly MethodInfo removePropertyMethod = GetMethod(
        nameof(DotNetRuntimeOperations.RemoveProperty),
        typeof(object),
        typeof(string),
        typeof(TextSpan)
    );

    private static readonly MethodInfo getElementMethod = GetMethod(
        nameof(DotNetRuntimeOperations.GetElement),
        typeof(object),
        typeof(object),
        typeof(bool),
        typeof(bool),
        typeof(TypeSymbol),
        typeof(TextSpan)
    );

    private static readonly MethodInfo setElementMethod = GetMethod(
        nameof(DotNetRuntimeOperations.SetElement),
        typeof(object),
        typeof(object),
        typeof(object),
        typeof(bool),
        typeof(TextSpan)
    );

    private static readonly MethodInfo removeElementPropertyMethod = GetMethod(
        nameof(DotNetRuntimeOperations.RemoveElementProperty),
        typeof(object),
        typeof(object),
        typeof(TextSpan)
    );

    private static readonly MethodInfo hasPropertyMethod = GetMethod(
        nameof(DotNetRuntimeOperations.HasProperty),
        typeof(object),
        typeof(object),
        typeof(TextSpan)
    );

    public static DotNetExportResult Export(
        IrProgram program,
        EnvironmentSchema environment
    )
    {
        DiagnosticCollection diagnostics = IrValidator.Validate(program, environment);

        if (diagnostics.HasErrors)
        {
            return new DotNetExportResult(null, diagnostics);
        }

        Func<DotNetRuntimeContext, object?> compiledDelegate = Compile(program);

        return new DotNetExportResult(compiledDelegate, DiagnosticCollection.Empty);
    }

    private static Func<DotNetRuntimeContext, object?> Compile(IrProgram program)
    {
        ParameterExpression context = Expression.Parameter(
            typeof(DotNetRuntimeContext),
            "context"
        );
        ParameterExpression[] slots =
            [ .. program.Slots.Select(static slot => Expression.Variable(typeof(object), $"slot{slot.Id}")) ];
        LabelTarget[] blockLabels =
            [ .. program.Blocks.Select(static block => Expression.Label($"block{block.Id}")) ];
        LabelTarget returnLabel = Expression.Label(typeof(object), "return");
        List<Expression> expressions = [ ];
        TextSpan entrySpan = program.Blocks[program.EntryBlock].Terminator.Span;
        expressions.Add(
            Expression.Call(
                validateEnvironmentMethod,
                context,
                Expression.Constant(program.EnvironmentFingerprint),
                Expression.Constant(entrySpan)
            )
        );
        expressions.Add(Expression.Goto(blockLabels[program.EntryBlock]));

        foreach (IrBasicBlock block in program.Blocks)
        {
            expressions.Add(Expression.Label(blockLabels[block.Id]));

            foreach (IrInstruction instruction in block.Instructions)
            {
                expressions.Add(CreateConsumeExpression(context, instruction.Span));
                expressions.Add(
                    CreateInstructionExpression(
                        context,
                        slots,
                        program.Slots,
                        instruction
                    )
                );
            }

            expressions.Add(CreateConsumeExpression(context, block.Terminator.Span));
            expressions.Add(
                CreateTerminatorExpression(
                    slots,
                    blockLabels,
                    returnLabel,
                    block.Terminator
                )
            );
        }

        expressions.Add(
            Expression.Label(
                returnLabel,
                Expression.Constant(null, typeof(object))
            )
        );
        BlockExpression body = Expression.Block(slots, expressions);
        Expression<Func<DotNetRuntimeContext, object?>> lambda =
            Expression.Lambda<Func<DotNetRuntimeContext, object?>>(body, context);

        return lambda.Compile();
    }

    private static Expression CreateInstructionExpression(
        ParameterExpression context,
        IReadOnlyList<ParameterExpression> slots,
        IReadOnlyList<IrSlot> slotMetadata,
        IrInstruction instruction
    )
    {
        return instruction switch
        {
            IrInstruction.Constant constant => Assign(
                slots,
                constant.Destination,
                Expression.Constant(constant.Value, typeof(object))
            ),
            IrInstruction.Copy copy => Assign(
                slots,
                copy.Destination,
                slots[copy.Source]
            ),
            IrInstruction.LoadGlobal global => Assign(
                slots,
                global.Destination,
                Expression.Call(
                    getGlobalMethod,
                    context,
                    Expression.Constant(global.GlobalId),
                    Expression.Constant(slotMetadata[global.Destination].Type),
                    Expression.Constant(global.Span)
                )
            ),
            IrInstruction.Unary unary => Assign(
                slots,
                unary.Destination,
                Expression.Call(
                    unaryMethod,
                    Expression.Constant(unary.Operator),
                    slots[unary.Operand],
                    Expression.Constant(unary.Span)
                )
            ),
            IrInstruction.Binary binary => Assign(
                slots,
                binary.Destination,
                Expression.Call(
                    binaryMethod,
                    context,
                    Expression.Constant(binary.Operator),
                    slots[binary.Left],
                    slots[binary.Right],
                    Expression.Constant(binary.Span)
                )
            ),
            IrInstruction.Convert conversion => Assign(
                slots,
                conversion.Destination,
                Expression.Call(
                    convertMethod,
                    context,
                    slots[conversion.Source],
                    Expression.Constant(conversion.TargetType),
                    Expression.Constant(conversion.Span)
                )
            ),
            IrInstruction.TypeTest typeTest => AssignBoxed(
                slots,
                typeTest.Destination,
                Expression.Call(
                    typeTestMethod,
                    context,
                    slots[typeTest.Source],
                    Expression.Constant(typeTest.TestedType),
                    Expression.Constant(typeTest.Span)
                )
            ),
            IrInstruction.IsNull isNull => AssignBoxed(
                slots,
                isNull.Destination,
                Expression.Call(isNullMethod, slots[isNull.Source])
            ),
            IrInstruction.HasProperty propertyTest => AssignBoxed(
                slots,
                propertyTest.Destination,
                Expression.Call(
                    hasPropertyMethod,
                    slots[propertyTest.Target],
                    slots[propertyTest.Key],
                    Expression.Constant(propertyTest.Span)
                )
            ),
            IrInstruction.CreateArray array => Assign(
                slots,
                array.Destination,
                Expression.Call(
                    createArrayMethod,
                    Expression.NewArrayInit(
                        typeof(object),
                        array.Elements.Select(element => slots[element])
                    )
                )
            ),
            IrInstruction.CreateObject objectValue => Assign(
                slots,
                objectValue.Destination,
                Expression.Call(
                    createObjectMethod,
                    Expression.NewArrayInit(
                        typeof(string),
                        objectValue.Properties.Select(
                            static property => Expression.Constant(property.Name)
                        )
                    ),
                    Expression.NewArrayInit(
                        typeof(object),
                        objectValue.Properties.Select(property => slots[property.Value])
                    )
                )
            ),
            IrInstruction.GetProperty property => Assign(
                slots,
                property.Destination,
                Expression.Call(
                    getPropertyMethod,
                    slots[property.Target],
                    Expression.Constant(property.Name),
                    Expression.Constant(property.IsOptional),
                    Expression.Constant(property.IsArrayLength),
                    Expression.Constant(slotMetadata[property.Destination].Type),
                    Expression.Constant(property.Span)
                )
            ),
            IrInstruction.SetProperty property => Expression.Call(
                setPropertyMethod,
                slots[property.Target],
                Expression.Constant(property.Name),
                slots[property.Value],
                Expression.Constant(property.Span)
            ),
            IrInstruction.RemoveProperty property => Expression.Call(
                removePropertyMethod,
                slots[property.Target],
                Expression.Constant(property.Name),
                Expression.Constant(property.Span)
            ),
            IrInstruction.GetElement element => Assign(
                slots,
                element.Destination,
                Expression.Call(
                    getElementMethod,
                    slots[element.Target],
                    slots[element.Index],
                    Expression.Constant(element.IsObjectAccess),
                    Expression.Constant(element.IsOptional),
                    Expression.Constant(slotMetadata[element.Destination].Type),
                    Expression.Constant(element.Span)
                )
            ),
            IrInstruction.SetElement element => Expression.Call(
                setElementMethod,
                slots[element.Target],
                slots[element.Index],
                slots[element.Value],
                Expression.Constant(element.IsObjectAccess),
                Expression.Constant(element.Span)
            ),
            IrInstruction.RemoveElementProperty property => Expression.Call(
                removeElementPropertyMethod,
                slots[property.Target],
                slots[property.Key],
                Expression.Constant(property.Span)
            ),
            IrInstruction.Call call => CreateCallExpression(context, slots, call),
            _ => throw new InvalidOperationException("Unknown IR instruction."),
        };
    }

    private static Expression CreateCallExpression(
        ParameterExpression context,
        IReadOnlyList<ParameterExpression> slots,
        IrInstruction.Call call
    )
    {
        MethodCallExpression invocation = Expression.Call(
            invokeMethod,
            context,
            Expression.Constant(call.FunctionId),
            Expression.NewArrayInit(
                typeof(object),
                call.Arguments.Select(argument => slots[argument])
            ),
            Expression.Constant(call.ReturnType),
            Expression.Constant(call.Span)
        );

        return call.Destination is null
            ? invocation
            : Assign(slots, call.Destination.Value, invocation);
    }

    private static Expression CreateTerminatorExpression(
        IReadOnlyList<ParameterExpression> slots,
        IReadOnlyList<LabelTarget> blockLabels,
        LabelTarget returnLabel,
        IrTerminator terminator
    )
    {
        return terminator switch
        {
            IrTerminator.Jump jump =>
                Expression.Goto(blockLabels[jump.TargetBlock]),
            IrTerminator.Branch branch =>
                Expression.IfThenElse(
                    Expression.Call(
                        requireBooleanMethod,
                        slots[branch.Condition],
                        Expression.Constant(branch.Span)
                    ),
                    Expression.Goto(blockLabels[branch.TrueBlock]),
                    Expression.Goto(blockLabels[branch.FalseBlock])
                ),
            IrTerminator.Return result =>
                Expression.Return(
                    returnLabel,
                    result.Value is null
                        ? Expression.Constant(null, typeof(object))
                        : slots[result.Value.Value]
                ),
            _ => throw new InvalidOperationException("Unknown IR terminator."),
        };
    }

    private static MethodCallExpression CreateConsumeExpression(
        ParameterExpression context,
        TextSpan span
    )
    {
        return Expression.Call(
            consumeMethod,
            context,
            Expression.Constant(span)
        );
    }

    private static BinaryExpression Assign(
        IReadOnlyList<ParameterExpression> slots,
        int destination,
        Expression value
    )
    {
        return Expression.Assign(slots[destination], value);
    }

    private static BinaryExpression AssignBoxed(
        IReadOnlyList<ParameterExpression> slots,
        int destination,
        Expression value
    )
    {
        return Assign(
            slots,
            destination,
            Expression.Convert(value, typeof(object))
        );
    }

    private static MethodInfo GetMethod(string name, params Type[] parameterTypes)
    {
        return typeof(DotNetRuntimeOperations).GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static,
            parameterTypes
        ) ?? throw new InvalidOperationException(
            $"Runtime operation '{name}' was not found."
        );
    }
}
