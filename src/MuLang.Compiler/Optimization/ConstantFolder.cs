using MuLang.Compiler.Binding;
using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core.Diagnostics;
using MuLang.Core.Evaluation;
using MuLang.Core.Types;

namespace MuLang.Compiler.Optimization;

internal sealed class ConstantFolder
{
    private readonly IList<Diagnostic> diagnostics = [ ];

    private ConstantFolder()
    {
    }

    public static ConstantFoldingResult Fold(BindingResult binding)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        ConstantFolder folder = new ();
        BoundRoot root = folder.FoldRoot(binding.Root);
        BindingResult foldedBinding = new (
            root,
            binding.Diagnostics,
            binding.CompilationMode,
            binding.LanguageProfileFingerprint
        );

        return new ConstantFoldingResult(
            foldedBinding,
            DiagnosticCollection.Create(folder.diagnostics)
        );
    }

    private BoundRoot FoldRoot(BoundRoot root)
    {
        return root switch
        {
            BoundRoot.Expression expression => new BoundRoot.Expression(
                expression.Syntax,
                FoldExpression(expression.Value)
            ),
            BoundRoot.Program program => new BoundRoot.Program(
                program.Syntax,
                program.Functions.Select(FoldFunction).ToArray(),
                program.Statements.Select(FoldStatement).ToArray(),
                program.ResultType
            ),
            _ => throw new InvalidOperationException("Unknown bound root."),
        };
    }

    private BoundFunction FoldFunction(BoundFunction function)
    {
        return new BoundFunction(
            function.Symbol,
            function.Statements.Select(FoldStatement).ToArray()
        );
    }

    private BoundStatement FoldStatement(BoundStatement statement)
    {
        return statement switch
        {
            BoundStatement.Block block => new BoundStatement.Block(
                block.Syntax,
                block.Statements.Select(FoldStatement).ToArray()
            ),
            BoundStatement.VariableDeclaration declaration =>
                new BoundStatement.VariableDeclaration(
                    declaration.Syntax,
                    declaration.Local,
                    declaration.Initializer is null
                        ? null
                        : FoldExpression(declaration.Initializer)
                ),
            BoundStatement.Assignment assignment => new BoundStatement.Assignment(
                assignment.Syntax,
                FoldExpression(assignment.Target),
                FoldExpression(assignment.Value)
            ),
            BoundStatement.Removal removal => new BoundStatement.Removal(
                removal.Syntax,
                FoldExpression(removal.Target)
            ),
            BoundStatement.ExpressionStatement expression =>
                new BoundStatement.ExpressionStatement(
                    expression.Syntax,
                    FoldExpression(expression.Value)
                ),
            BoundStatement.If conditional => FoldIf(conditional),
            BoundStatement.While loop => FoldWhile(loop),
            BoundStatement.For loop => FoldFor(loop),
            BoundStatement.Return result => new BoundStatement.Return(
                result.Syntax,
                result.Value is null
                    ? null
                    : FoldExpression(result.Value)
            ),
            BoundStatement.Break or
                BoundStatement.Continue or
                BoundStatement.Empty => statement,
            _ => throw new InvalidOperationException("Unknown bound statement."),
        };
    }

    private BoundStatement FoldIf(BoundStatement.If statement)
    {
        BoundExpression condition = FoldExpression(statement.Condition);
        bool? value = GetBooleanConstant(condition);

        return new BoundStatement.If(
            statement.Syntax,
            condition,
            value is false
                ? statement.Then
                : FoldStatement(statement.Then),
            statement.Else is null || value is true
                ? statement.Else
                : FoldStatement(statement.Else)
        );
    }

    private BoundStatement FoldWhile(BoundStatement.While statement)
    {
        BoundExpression condition = FoldExpression(statement.Condition);

        return new BoundStatement.While(
            statement.Syntax,
            condition,
            GetBooleanConstant(condition) is false
                ? statement.Body
                : FoldStatement(statement.Body)
        );
    }

    private BoundStatement FoldFor(BoundStatement.For statement)
    {
        BoundStatement? initializer = statement.Initializer is null
            ? null
            : FoldStatement(statement.Initializer);
        BoundExpression? condition = statement.Condition is null
            ? null
            : FoldExpression(statement.Condition);
        bool skipsIterations = GetBooleanConstant(condition) is false;

        return new BoundStatement.For(
            statement.Syntax,
            initializer,
            condition,
            statement.Iterator is null || skipsIterations
                ? statement.Iterator
                : FoldStatement(statement.Iterator),
            skipsIterations
                ? statement.Body
                : FoldStatement(statement.Body)
        );
    }

    private BoundExpression FoldExpression(BoundExpression expression)
    {
        return expression switch
        {
            BoundExpression.Literal or
                BoundExpression.Local or
                BoundExpression.Parameter or
                BoundExpression.Global or
                BoundExpression.Error => expression,
            BoundExpression.Array array => new BoundExpression.Array(
                array.Syntax,
                array.ArrayType,
                array.Elements.Select(FoldExpression).ToArray()
            ),
            BoundExpression.Object objectValue => new BoundExpression.Object(
                objectValue.Syntax,
                objectValue.ObjectType,
                objectValue.Properties
                    .Select(
                        property => new BoundExpression.ObjectProperty(
                            property.Name,
                            FoldExpression(property.Value)
                        )
                    )
                    .ToArray()
            ),
            BoundExpression.Unary unary => FoldUnary(unary),
            BoundExpression.Binary binary => FoldBinary(binary),
            BoundExpression.Coalescing coalescing => FoldCoalescing(coalescing),
            BoundExpression.Conversion conversion => FoldConversion(conversion),
            BoundExpression.Truthiness truthiness => FoldTruthiness(truthiness),
            BoundExpression.TypeTest typeTest => FoldTypeTest(typeTest),
            BoundExpression.PropertyTest propertyTest =>
                new BoundExpression.PropertyTest(
                    propertyTest.Syntax,
                    FoldExpression(propertyTest.Target),
                    FoldExpression(propertyTest.Key)
                ),
            BoundExpression.Conditional conditional => FoldConditional(conditional),
            BoundExpression.ProviderCall call => new BoundExpression.ProviderCall(
                call.Syntax,
                call.Function,
                call.Arguments.Select(FoldExpression).ToArray()
            ),
            BoundExpression.UserCall call => new BoundExpression.UserCall(
                call.Syntax,
                call.Function,
                call.Arguments.Select(FoldExpression).ToArray()
            ),
            BoundExpression.MemberAccess member => new BoundExpression.MemberAccess(
                member.Syntax,
                member.Type,
                FoldExpression(member.Target),
                member.Name,
                member.Property,
                member.IsDynamic,
                member.IsOptional,
                member.IsArrayLength
            ),
            BoundExpression.ElementAccess element => FoldElementAccess(element),
            _ => throw new InvalidOperationException("Unknown bound expression."),
        };
    }

    private BoundExpression FoldUnary(BoundExpression.Unary expression)
    {
        BoundExpression operand = FoldExpression(expression.Operand);

        if (operand is not BoundExpression.Literal literal)
        {
            return new BoundExpression.Unary(
                expression.Syntax,
                expression.Type,
                expression.Operator,
                operand
            );
        }

        return Evaluate(
            expression,
            () => PrimitiveValueOperations.EvaluateUnary(
                MapUnaryOperator(expression.Operator),
                literal.Value
            )
        );
    }

    private BoundExpression FoldBinary(BoundExpression.Binary expression)
    {
        BoundExpression left = FoldExpression(expression.Left);

        if (
            expression.Operator is
                TokenKind.AmpersandAmpersand or TokenKind.PipePipe &&
            left is BoundExpression.Literal { Value: bool leftValue }
        )
        {
            bool isAnd = expression.Operator == TokenKind.AmpersandAmpersand;

            if (isAnd ? !leftValue : leftValue)
            {
                return CreateLiteral(expression, isAnd ? false : true);
            }

            BoundExpression selectedRight = FoldExpression(expression.Right);

            return selectedRight is BoundExpression.Literal { Value: bool rightValue }
                ? CreateLiteral(expression, rightValue)
                : selectedRight;
        }

        BoundExpression right = FoldExpression(expression.Right);

        if (
            left is not BoundExpression.Literal leftLiteral ||
            right is not BoundExpression.Literal rightLiteral
        )
        {
            return new BoundExpression.Binary(
                expression.Syntax,
                expression.Type,
                left,
                expression.Operator,
                right
            );
        }

        return Evaluate(
            expression,
            () => PrimitiveValueOperations.EvaluateBinary(
                MapBinaryOperator(expression.Operator),
                leftLiteral.Value,
                rightLiteral.Value
            )
        );
    }

    private BoundExpression FoldCoalescing(BoundExpression.Coalescing expression)
    {
        BoundExpression left = FoldExpression(expression.Left);

        if (left is BoundExpression.Literal literal)
        {
            if (literal.Value is null)
            {
                return FoldExpression(expression.Right);
            }

            return Evaluate(
                expression,
                () => PrimitiveValueOperations.ConvertValue(
                    literal.Value,
                    expression.Type
                )
            );
        }

        return new BoundExpression.Coalescing(
            expression.Syntax,
            expression.Type,
            left,
            FoldExpression(expression.Right)
        );
    }

    private BoundExpression FoldConversion(BoundExpression.Conversion expression)
    {
        BoundExpression operand = FoldExpression(expression.Expression);

        if (
            operand is not BoundExpression.Literal literal ||
            !PrimitiveValueOperations.IsPrimitiveType(expression.Type)
        )
        {
            return new BoundExpression.Conversion(
                expression.Syntax,
                expression.Type,
                operand,
                expression.ConversionKind,
                expression.IsCast
            );
        }

        if (expression.IsCast)
        {
            if (PrimitiveValueOperations.IsValueOfType(literal.Value, expression.Type))
            {
                return CreateLiteral(expression, literal.Value);
            }

            return ReportFailure(
                expression,
                $"Runtime value cannot be cast to '{expression.Type.DisplayName}'."
            );
        }

        return Evaluate(
            expression,
            () => PrimitiveValueOperations.ConvertValue(
                literal.Value,
                expression.Type
            )
        );
    }

    private BoundExpression FoldTruthiness(BoundExpression.Truthiness expression)
    {
        BoundExpression operand = FoldExpression(expression.Expression);

        if (
            operand is BoundExpression.Literal literal &&
            PrimitiveValueOperations.TryGetTruthiness(literal.Value, out bool value)
        )
        {
            return CreateLiteral(expression, value);
        }

        return new BoundExpression.Truthiness(expression.Syntax, operand);
    }

    private BoundExpression FoldTypeTest(BoundExpression.TypeTest expression)
    {
        BoundExpression operand = FoldExpression(expression.Expression);

        if (
            operand is BoundExpression.Literal literal &&
            PrimitiveValueOperations.IsPrimitiveType(expression.TestedType)
        )
        {
            return CreateLiteral(
                expression,
                PrimitiveValueOperations.IsValueOfType(
                    literal.Value,
                    expression.TestedType
                )
            );
        }

        return new BoundExpression.TypeTest(
            expression.Syntax,
            operand,
            expression.TestedType
        );
    }

    private BoundExpression FoldConditional(BoundExpression.Conditional expression)
    {
        BoundExpression condition = FoldExpression(expression.Condition);

        if (condition is BoundExpression.Literal { Value: bool value })
        {
            return FoldExpression(value ? expression.WhenTrue : expression.WhenFalse);
        }

        return new BoundExpression.Conditional(
            expression.Syntax,
            expression.Type,
            condition,
            FoldExpression(expression.WhenTrue),
            FoldExpression(expression.WhenFalse)
        );
    }

    private BoundExpression FoldElementAccess(BoundExpression.ElementAccess expression)
    {
        BoundExpression target = FoldExpression(expression.Target);
        BoundExpression index =
            expression is { IsOptional: true, Target.Type: NullableTypeSymbol } &&
            target is BoundExpression.Literal { Value: null }
                ? expression.Index
                : FoldExpression(expression.Index);

        return new BoundExpression.ElementAccess(
            expression.Syntax,
            expression.Type,
            target,
            index,
            expression.Property,
            expression.IsObjectAccess,
            expression.IsDynamic,
            expression.IsOptional
        );
    }

    private BoundExpression Evaluate(
        BoundExpression expression,
        Func<object?> evaluate
    )
    {
        try
        {
            return CreateLiteral(expression, evaluate());
        }
        catch (PrimitiveOperationException exception)
        {
            diagnostics.Add(
                new Diagnostic(
                    DiagnosticCodes.ConstantEvaluationFailed,
                    DiagnosticSeverity.Error,
                    DiagnosticCategory.Type,
                    expression.Span,
                    $"Constant expression cannot be evaluated: {exception.Message}"
                )
            );

            return new BoundExpression.Error(expression.Syntax);
        }
    }

    private BoundExpression ReportFailure(
        BoundExpression expression,
        string message
    )
    {
        diagnostics.Add(
            new Diagnostic(
                DiagnosticCodes.ConstantEvaluationFailed,
                DiagnosticSeverity.Error,
                DiagnosticCategory.Type,
                expression.Span,
                $"Constant expression cannot be evaluated: {message}"
            )
        );

        return new BoundExpression.Error(expression.Syntax);
    }

    private static BoundExpression.Literal CreateLiteral(
        BoundExpression expression,
        object? value
    )
    {
        return new BoundExpression.Literal(
            expression.Syntax,
            expression.Type,
            value
        );
    }

    private static bool? GetBooleanConstant(BoundExpression? expression)
    {
        return expression is BoundExpression.Literal { Value: bool value }
            ? value
            : null;
    }

    private static PrimitiveUnaryOperation MapUnaryOperator(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Plus => PrimitiveUnaryOperation.Identity,
            TokenKind.Minus => PrimitiveUnaryOperation.Negate,
            TokenKind.Bang => PrimitiveUnaryOperation.LogicalNot,
            TokenKind.Tilde => PrimitiveUnaryOperation.BitwiseNot,
            _ => throw new InvalidOperationException("Unknown unary operator."),
        };
    }

    private static PrimitiveBinaryOperation MapBinaryOperator(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Plus => PrimitiveBinaryOperation.Add,
            TokenKind.Minus => PrimitiveBinaryOperation.Subtract,
            TokenKind.Asterisk => PrimitiveBinaryOperation.Multiply,
            TokenKind.Slash => PrimitiveBinaryOperation.Divide,
            TokenKind.Percent => PrimitiveBinaryOperation.Remainder,
            TokenKind.LeftShift => PrimitiveBinaryOperation.LeftShift,
            TokenKind.RightShift => PrimitiveBinaryOperation.RightShift,
            TokenKind.LessThan => PrimitiveBinaryOperation.LessThan,
            TokenKind.LessThanOrEqual => PrimitiveBinaryOperation.LessThanOrEqual,
            TokenKind.GreaterThan => PrimitiveBinaryOperation.GreaterThan,
            TokenKind.GreaterThanOrEqual => PrimitiveBinaryOperation.GreaterThanOrEqual,
            TokenKind.EqualEqual => PrimitiveBinaryOperation.StructuralEqual,
            TokenKind.BangEqual => PrimitiveBinaryOperation.StructuralNotEqual,
            TokenKind.EqualEqualEqual => PrimitiveBinaryOperation.IdentityEqual,
            TokenKind.BangEqualEqual => PrimitiveBinaryOperation.IdentityNotEqual,
            TokenKind.Ampersand => PrimitiveBinaryOperation.BitwiseAnd,
            TokenKind.Caret => PrimitiveBinaryOperation.BitwiseXor,
            TokenKind.Pipe => PrimitiveBinaryOperation.BitwiseOr,
            _ => throw new InvalidOperationException("Unknown binary operator."),
        };
    }
}
