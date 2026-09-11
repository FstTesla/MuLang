using MuLang.Compiler.Binding;
using MuLang.Compiler.Syntax;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal sealed class Lowerer
{
    private readonly EnvironmentSchema environment;
    private readonly IrBuilder builder = new ();

    private readonly IDictionary<LocalSymbol, int> localSlots =
        new Dictionary<LocalSymbol, int>(ReferenceEqualityComparer.Instance);

    private readonly Stack<LoweringLoopContext> loopContexts = [ ];

    private Lowerer(EnvironmentSchema environment)
    {
        this.environment = environment;
    }

    public static LoweringResult Lower(
        BindingResult bindingResult,
        EnvironmentSchema environment
    )
    {
        if (bindingResult is null)
        {
            throw new ArgumentNullException(nameof(bindingResult));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (bindingResult.Diagnostics.HasErrors)
        {
            return new LoweringResult(null, bindingResult.Diagnostics);
        }

        Lowerer lowerer = new (environment);
        TypeSymbol resultType = lowerer.LowerRoot(bindingResult.Root);
        IrProgram program = lowerer.builder.Build(
            environment.Fingerprint,
            resultType
        );

        return new LoweringResult(program, DiagnosticCollection.Empty);
    }

    private TypeSymbol LowerRoot(BoundRoot root)
    {
        switch (root)
        {
            case BoundRoot.Expression expression:
            {
                int value = LowerExpression(expression.Value);
                builder.Terminate(new IrTerminator.Return(expression.Span, value));
                return expression.Value.Type;
            }

            case BoundRoot.Program program:
            {
                LowerStatements(program.Statements);

                if (builder.CurrentBlock is not null)
                {
                    builder.Terminate(new IrTerminator.Return(program.Span, null));
                }

                return program.ResultType;
            }

            default:
            {
                throw new InvalidOperationException("Unknown bound root.");
            }
        }
    }

    private void LowerStatements(IEnumerable<BoundStatement> statements)
    {
        foreach (BoundStatement statement in statements)
        {
            if (builder.CurrentBlock is null)
            {
                break;
            }

            LowerStatement(statement);
        }
    }

    private void LowerStatement(BoundStatement statement)
    {
        switch (statement)
        {
            case BoundStatement.Block block:
            {
                LowerStatements(block.Statements);
                break;
            }

            case BoundStatement.VariableDeclaration declaration:
            {
                int localSlot = GetLocalSlot(declaration.Local);

                if (declaration.Initializer is not null)
                {
                    int initializer = LowerExpression(declaration.Initializer);
                    builder.Emit(
                        new IrInstruction.Copy(
                            declaration.Span,
                            localSlot,
                            initializer
                        )
                    );
                }

                break;
            }

            case BoundStatement.Assignment assignment:
            {
                LowerAssignment(assignment);
                break;
            }

            case BoundStatement.Removal removal:
            {
                LowerRemoval(removal);
                break;
            }

            case BoundStatement.ExpressionStatement expression:
            {
                LowerExpression(expression.Value);
                break;
            }

            case BoundStatement.If conditional:
            {
                LowerIf(conditional);
                break;
            }

            case BoundStatement.While loop:
            {
                LowerWhile(loop);
                break;
            }

            case BoundStatement.For loop:
            {
                LowerFor(loop);
                break;
            }

            case BoundStatement.Break loopExit:
            {
                LowerBreak(loopExit);
                break;
            }

            case BoundStatement.Continue iteration:
            {
                LowerContinue(iteration);
                break;
            }

            case BoundStatement.Return result:
            {
                int? value = result.Value is null
                    ? null
                    : LowerExpression(result.Value);
                builder.Terminate(new IrTerminator.Return(result.Span, value));
                builder.SetUnreachable();
                break;
            }

            case BoundStatement.Empty:
            {
                break;
            }

            default:
            {
                throw new InvalidOperationException("Unknown bound statement.");
            }
        }
    }

    private void LowerAssignment(BoundStatement.Assignment assignment)
    {
        switch (assignment.Target)
        {
            case BoundExpression.Local local:
            {
                int value = LowerExpression(assignment.Value);
                builder.Emit(
                    new IrInstruction.Copy(
                        assignment.Span,
                        GetLocalSlot(local.Symbol),
                        value
                    )
                );
                break;
            }

            case BoundExpression.MemberAccess member:
            {
                int target = LowerExpression(member.Target);
                int value = LowerExpression(assignment.Value);
                builder.Emit(
                    new IrInstruction.SetProperty(
                        assignment.Span,
                        target,
                        member.Name,
                        value
                    )
                );
                break;
            }

            case BoundExpression.ElementAccess element:
            {
                int target = LowerExpression(element.Target);
                int index = LowerExpression(element.Index);
                int value = LowerExpression(assignment.Value);
                builder.Emit(
                    new IrInstruction.SetElement(
                        assignment.Span,
                        target,
                        index,
                        value,
                        element.IsObjectAccess
                    )
                );
                break;
            }

            default:
            {
                throw new InvalidOperationException("Unknown assignment target.");
            }
        }
    }

    private void LowerRemoval(BoundStatement.Removal removal)
    {
        switch (removal.Target)
        {
            case BoundExpression.MemberAccess member:
            {
                int target = LowerExpression(member.Target);
                builder.Emit(
                    new IrInstruction.RemoveProperty(
                        removal.Span,
                        target,
                        member.Name
                    )
                );
                break;
            }

            case BoundExpression.ElementAccess element:
            {
                int target = LowerExpression(element.Target);
                int key = LowerExpression(element.Index);
                builder.Emit(
                    new IrInstruction.RemoveElementProperty(
                        removal.Span,
                        target,
                        key
                    )
                );
                break;
            }

            default:
            {
                throw new InvalidOperationException("Unknown removal target.");
            }
        }
    }

    private void LowerIf(BoundStatement.If conditional)
    {
        int condition = LowerExpression(conditional.Condition);
        int thenBlock = builder.CreateBlock();
        int elseBlock = builder.CreateBlock();
        builder.Terminate(
            new IrTerminator.Branch(
                conditional.Condition.Span,
                condition,
                thenBlock,
                elseBlock
            )
        );

        builder.SwitchTo(thenBlock);
        LowerStatement(conditional.Then);
        MutableIrBlock? thenEnd = builder.CurrentBlock;

        builder.SwitchTo(elseBlock);

        if (conditional.Else is not null)
        {
            LowerStatement(conditional.Else);
        }

        MutableIrBlock? elseEnd = builder.CurrentBlock;

        if (thenEnd is null && elseEnd is null)
        {
            builder.SetUnreachable();
            return;
        }

        int mergeBlock = builder.CreateBlock();

        if (thenEnd is not null)
        {
            builder.SwitchTo(thenEnd.Id);
            builder.Terminate(new IrTerminator.Jump(conditional.Span, mergeBlock));
        }

        if (elseEnd is not null)
        {
            builder.SwitchTo(elseEnd.Id);
            builder.Terminate(new IrTerminator.Jump(conditional.Span, mergeBlock));
        }

        builder.SwitchTo(mergeBlock);
    }

    private void LowerWhile(BoundStatement.While loop)
    {
        int conditionBlock = builder.CreateBlock();
        int bodyBlock = builder.CreateBlock();
        builder.Terminate(new IrTerminator.Jump(loop.Span, conditionBlock));
        builder.SwitchTo(conditionBlock);
        int condition = LowerExpression(loop.Condition);
        bool isInfinite = IsConstantTrue(loop.Condition);
        int? exitBlock = isInfinite
            ? null
            : builder.CreateBlock();

        if (isInfinite)
        {
            builder.Terminate(new IrTerminator.Jump(loop.Condition.Span, bodyBlock));
        }
        else
        {
            int finiteExitBlock = exitBlock ??
                throw new InvalidOperationException("A finite loop requires an exit block.");
            builder.Terminate(
                new IrTerminator.Branch(
                    loop.Condition.Span,
                    condition,
                    bodyBlock,
                    finiteExitBlock
                )
            );
        }

        LoweringLoopContext context = new (
            conditionBlock,
            exitBlock,
            builder.CreateBlock
        );
        loopContexts.Push(context);
        builder.SwitchTo(bodyBlock);
        LowerStatement(loop.Body);

        if (builder.CurrentBlock is not null)
        {
            builder.Terminate(new IrTerminator.Jump(loop.Span, conditionBlock));
        }

        loopContexts.Pop();

        if (isInfinite && !context.HasBreakBlock)
        {
            builder.SetUnreachable();
        }
        else
        {
            builder.SwitchTo(context.BreakBlock);
        }
    }

    private void LowerFor(BoundStatement.For loop)
    {
        if (loop.Initializer is not null)
        {
            LowerStatement(loop.Initializer);
        }

        int conditionBlock = builder.CreateBlock();
        int bodyBlock = builder.CreateBlock();
        int iteratorBlock = builder.CreateBlock();
        builder.Terminate(new IrTerminator.Jump(loop.Span, conditionBlock));
        builder.SwitchTo(conditionBlock);
        bool isInfinite = loop.Condition is null || IsConstantTrue(loop.Condition);
        int? exitBlock = isInfinite
            ? null
            : builder.CreateBlock();

        if (isInfinite)
        {
            builder.Terminate(new IrTerminator.Jump(loop.Span, bodyBlock));
        }
        else
        {
            BoundExpression conditionExpression = loop.Condition ??
                throw new InvalidOperationException("A finite for loop requires a condition.");
            int finiteExitBlock = exitBlock ??
                throw new InvalidOperationException("A finite for loop requires an exit block.");
            int condition = LowerExpression(conditionExpression);
            builder.Terminate(
                new IrTerminator.Branch(
                    conditionExpression.Span,
                    condition,
                    bodyBlock,
                    finiteExitBlock
                )
            );
        }

        LoweringLoopContext context = new (
            iteratorBlock,
            exitBlock,
            builder.CreateBlock
        );
        loopContexts.Push(context);
        builder.SwitchTo(bodyBlock);
        LowerStatement(loop.Body);

        if (builder.CurrentBlock is not null)
        {
            builder.Terminate(new IrTerminator.Jump(loop.Span, iteratorBlock));
        }

        builder.SwitchTo(iteratorBlock);

        if (loop.Iterator is not null)
        {
            LowerStatement(loop.Iterator);
        }

        if (builder.CurrentBlock is not null)
        {
            builder.Terminate(new IrTerminator.Jump(loop.Span, conditionBlock));
        }

        loopContexts.Pop();

        if (isInfinite && !context.HasBreakBlock)
        {
            builder.SetUnreachable();
        }
        else
        {
            builder.SwitchTo(context.BreakBlock);
        }
    }

    private void LowerBreak(BoundStatement.Break statement)
    {
        LoweringLoopContext context = loopContexts.ElementAt(statement.Level - 1);
        builder.Terminate(new IrTerminator.Jump(statement.Span, context.BreakBlock));
        builder.SetUnreachable();
    }

    private void LowerContinue(BoundStatement.Continue statement)
    {
        LoweringLoopContext context = loopContexts.ElementAt(statement.Level - 1);
        builder.Terminate(new IrTerminator.Jump(statement.Span, context.ContinueBlock));
        builder.SetUnreachable();
    }

    private int LowerExpression(BoundExpression expression)
    {
        return expression switch
        {
            BoundExpression.Literal literal => LowerLiteral(literal),
            BoundExpression.Local local => GetLocalSlot(local.Symbol),
            BoundExpression.Global global => LowerGlobal(global),
            BoundExpression.Array array => LowerArray(array),
            BoundExpression.Object objectValue => LowerObject(objectValue),
            BoundExpression.Unary unary => LowerUnary(unary),
            BoundExpression.Binary binary => LowerBinary(binary),
            BoundExpression.Conversion conversion => LowerConversion(conversion),
            BoundExpression.TypeTest typeTest => LowerTypeTest(typeTest),
            BoundExpression.PropertyTest propertyTest => LowerPropertyTest(propertyTest),
            BoundExpression.Conditional conditional => LowerConditional(conditional),
            BoundExpression.Call call => LowerCall(call),
            BoundExpression.MemberAccess member => LowerMemberAccess(member),
            BoundExpression.ElementAccess element => LowerElementAccess(element),
            BoundExpression.Error => throw new InvalidOperationException(
                "An erroneous expression cannot be lowered."
            ),
            _ => throw new InvalidOperationException("Unknown bound expression."),
        };
    }

    private int LowerLiteral(BoundExpression.Literal expression)
    {
        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.Constant(
                expression.Span,
                destination,
                expression.Type,
                expression.Value
            )
        );

        return destination;
    }

    private int LowerGlobal(BoundExpression.Global expression)
    {
        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.LoadGlobal(
                expression.Span,
                destination,
                expression.Symbol.Id
            )
        );

        return destination;
    }

    private int LowerArray(BoundExpression.Array expression)
    {
        IList<int> elements = [ ];

        foreach (BoundExpression element in expression.Elements)
        {
            elements.Add(LowerExpression(element));
        }

        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.CreateArray(
                expression.Span,
                destination,
                expression.ArrayType,
                elements.AsReadOnly()
            )
        );

        return destination;
    }

    private int LowerObject(BoundExpression.Object expression)
    {
        IList<IrInstruction.ObjectPropertyValue> properties = [ ];

        foreach (BoundExpression.ObjectProperty property in expression.Properties)
        {
            properties.Add(
                new IrInstruction.ObjectPropertyValue(
                    property.Name,
                    LowerExpression(property.Value)
                )
            );
        }

        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.CreateObject(
                expression.Span,
                destination,
                expression.ObjectType,
                properties.AsReadOnly()
            )
        );

        return destination;
    }

    private int LowerUnary(BoundExpression.Unary expression)
    {
        int operand = LowerExpression(expression.Operand);
        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.Unary(
                expression.Span,
                destination,
                MapUnaryOperator(expression.Operator),
                operand
            )
        );

        return destination;
    }

    private int LowerBinary(BoundExpression.Binary expression)
    {
        if (expression.Operator is TokenKind.AmpersandAmpersand or TokenKind.PipePipe)
        {
            return LowerShortCircuit(expression);
        }

        int left = LowerExpression(expression.Left);
        int right = LowerExpression(expression.Right);
        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.Binary(
                expression.Span,
                destination,
                MapBinaryOperator(expression.Operator),
                left,
                right
            )
        );

        return destination;
    }

    private int LowerShortCircuit(BoundExpression.Binary expression)
    {
        int result = CreateTemporary(expression.Type);
        int left = LowerExpression(expression.Left);
        int rightBlock = builder.CreateBlock();
        int shortCircuitBlock = builder.CreateBlock();
        int mergeBlock = builder.CreateBlock();
        bool isAnd = expression.Operator == TokenKind.AmpersandAmpersand;
        builder.Terminate(
            new IrTerminator.Branch(
                expression.Left.Span,
                left,
                isAnd ? rightBlock : shortCircuitBlock,
                isAnd ? shortCircuitBlock : rightBlock
            )
        );

        builder.SwitchTo(shortCircuitBlock);
        int shortCircuitValue = CreateTemporary(TypeSymbols.Bool);
        builder.Emit(
            new IrInstruction.Constant(
                expression.Span,
                shortCircuitValue,
                TypeSymbols.Bool,
                !isAnd
            )
        );
        builder.Emit(
            new IrInstruction.Copy(
                expression.Span,
                result,
                shortCircuitValue
            )
        );
        builder.Terminate(new IrTerminator.Jump(expression.Span, mergeBlock));

        builder.SwitchTo(rightBlock);
        int right = LowerExpression(expression.Right);
        builder.Emit(new IrInstruction.Copy(expression.Span, result, right));
        builder.Terminate(new IrTerminator.Jump(expression.Span, mergeBlock));
        builder.SwitchTo(mergeBlock);

        return result;
    }

    private int LowerConversion(BoundExpression.Conversion expression)
    {
        int sourceSlot = LowerExpression(expression.Expression);
        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.Convert(
                expression.Span,
                destination,
                sourceSlot,
                expression.Type,
                expression.ConversionKind == ConversionKind.Checked
            )
        );

        return destination;
    }

    private int LowerTypeTest(BoundExpression.TypeTest expression)
    {
        int sourceSlot = LowerExpression(expression.Expression);
        int destination = CreateTemporary(TypeSymbols.Bool);
        builder.Emit(
            new IrInstruction.TypeTest(
                expression.Span,
                destination,
                sourceSlot,
                expression.TestedType
            )
        );

        return destination;
    }

    private int LowerPropertyTest(BoundExpression.PropertyTest expression)
    {
        int target = LowerExpression(expression.Target);
        int key = LowerExpression(expression.Key);
        int destination = CreateTemporary(TypeSymbols.Bool);
        builder.Emit(
            new IrInstruction.HasProperty(
                expression.Span,
                destination,
                target,
                key
            )
        );

        return destination;
    }

    private int LowerConditional(BoundExpression.Conditional expression)
    {
        int result = CreateTemporary(expression.Type);
        int condition = LowerExpression(expression.Condition);
        int trueBlock = builder.CreateBlock();
        int falseBlock = builder.CreateBlock();
        int mergeBlock = builder.CreateBlock();
        builder.Terminate(
            new IrTerminator.Branch(
                expression.Condition.Span,
                condition,
                trueBlock,
                falseBlock
            )
        );

        builder.SwitchTo(trueBlock);
        int trueValue = LowerExpression(expression.WhenTrue);
        builder.Emit(new IrInstruction.Copy(expression.WhenTrue.Span, result, trueValue));
        builder.Terminate(new IrTerminator.Jump(expression.Span, mergeBlock));

        builder.SwitchTo(falseBlock);
        int falseValue = LowerExpression(expression.WhenFalse);
        builder.Emit(new IrInstruction.Copy(expression.WhenFalse.Span, result, falseValue));
        builder.Terminate(new IrTerminator.Jump(expression.Span, mergeBlock));
        builder.SwitchTo(mergeBlock);

        return result;
    }

    private int LowerCall(BoundExpression.Call expression)
    {
        IList<int> arguments = [ ];

        foreach (BoundExpression argument in expression.Arguments)
        {
            arguments.Add(LowerExpression(argument));
        }

        int? destination = expression.Type.Kind == TypeKind.Void
            ? null
            : CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.Call(
                expression.Span,
                destination,
                expression.Function.Id,
                expression.Type,
                arguments.AsReadOnly()
            )
        );

        return destination ?? -1;
    }

    private int LowerMemberAccess(BoundExpression.MemberAccess expression)
    {
        int target = LowerExpression(expression.Target);
        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.GetProperty(
                expression.Span,
                destination,
                target,
                expression.Name,
                expression.IsOptional,
                expression.IsArrayLength
            )
        );

        return destination;
    }

    private int LowerElementAccess(BoundExpression.ElementAccess expression)
    {
        int target = LowerExpression(expression.Target);

        if (expression is { IsOptional: true, Target.Type: NullableTypeSymbol })
        {
            return LowerOptionalElementAccess(expression, target);
        }

        int index = LowerExpression(expression.Index);
        int destination = CreateTemporary(expression.Type);
        builder.Emit(
            new IrInstruction.GetElement(
                expression.Span,
                destination,
                target,
                index,
                expression.IsObjectAccess,
                expression.IsOptional
            )
        );

        return destination;
    }

    private int LowerOptionalElementAccess(
        BoundExpression.ElementAccess expression,
        int target
    )
    {
        int result = CreateTemporary(expression.Type);
        int isNull = CreateTemporary(TypeSymbols.Bool);
        builder.Emit(
            new IrInstruction.IsNull(
                expression.Target.Span,
                isNull,
                target
            )
        );
        int nullBlock = builder.CreateBlock();
        int valueBlock = builder.CreateBlock();
        int mergeBlock = builder.CreateBlock();
        builder.Terminate(
            new IrTerminator.Branch(
                expression.Target.Span,
                isNull,
                nullBlock,
                valueBlock
            )
        );

        builder.SwitchTo(nullBlock);
        builder.Emit(
            new IrInstruction.Constant(
                expression.Span,
                result,
                expression.Type,
                null
            )
        );
        builder.Terminate(new IrTerminator.Jump(expression.Span, mergeBlock));

        builder.SwitchTo(valueBlock);
        int index = LowerExpression(expression.Index);
        builder.Emit(
            new IrInstruction.GetElement(
                expression.Span,
                result,
                target,
                index,
                expression.IsObjectAccess,
                true
            )
        );
        builder.Terminate(new IrTerminator.Jump(expression.Span, mergeBlock));
        builder.SwitchTo(mergeBlock);

        return result;
    }

    private int GetLocalSlot(LocalSymbol local)
    {
        if (localSlots.TryGetValue(local, out int slot))
        {
            return slot;
        }

        slot = builder.CreateSlot(IrSlotKind.Local, local.Type, local.Name);
        localSlots.Add(local, slot);

        return slot;
    }

    private int CreateTemporary(TypeSymbol type)
    {
        return builder.CreateSlot(IrSlotKind.Temporary, type);
    }

    private static bool IsConstantTrue(BoundExpression expression)
    {
        return expression is BoundExpression.Literal
        {
            Type.Kind: TypeKind.Bool,
            Value: true,
        };
    }

    private static IrUnaryOperator MapUnaryOperator(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Plus => IrUnaryOperator.Identity,
            TokenKind.Minus => IrUnaryOperator.Negate,
            TokenKind.Bang => IrUnaryOperator.LogicalNot,
            TokenKind.Tilde => IrUnaryOperator.BitwiseNot,
            _ => throw new InvalidOperationException($"Unsupported unary operator '{kind}'."),
        };
    }

    private static IrBinaryOperator MapBinaryOperator(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Plus => IrBinaryOperator.Add,
            TokenKind.Minus => IrBinaryOperator.Subtract,
            TokenKind.Asterisk => IrBinaryOperator.Multiply,
            TokenKind.Slash => IrBinaryOperator.Divide,
            TokenKind.Percent => IrBinaryOperator.Remainder,
            TokenKind.LeftShift => IrBinaryOperator.LeftShift,
            TokenKind.RightShift => IrBinaryOperator.RightShift,
            TokenKind.LessThan => IrBinaryOperator.LessThan,
            TokenKind.LessThanOrEqual => IrBinaryOperator.LessThanOrEqual,
            TokenKind.GreaterThan => IrBinaryOperator.GreaterThan,
            TokenKind.GreaterThanOrEqual => IrBinaryOperator.GreaterThanOrEqual,
            TokenKind.EqualEqual => IrBinaryOperator.StructuralEqual,
            TokenKind.BangEqual => IrBinaryOperator.StructuralNotEqual,
            TokenKind.EqualEqualEqual => IrBinaryOperator.IdentityEqual,
            TokenKind.BangEqualEqual => IrBinaryOperator.IdentityNotEqual,
            TokenKind.Ampersand => IrBinaryOperator.BitwiseAnd,
            TokenKind.Caret => IrBinaryOperator.BitwiseXor,
            TokenKind.Pipe => IrBinaryOperator.BitwiseOr,
            _ => throw new InvalidOperationException($"Unsupported binary operator '{kind}'."),
        };
    }
}
