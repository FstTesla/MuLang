using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Globalization;

namespace MuLang.Compiler.Binding;

internal sealed class Binder
{
    private readonly SourceText source;
    private readonly EnvironmentSchema environment;
    private readonly TypeSymbol expectedResultType;
    private readonly List<Diagnostic> diagnostics = [ ];
    private BindingScope scope = new (null);
    private FlowState currentFlowState = new ([ ]);
    private readonly Stack<LoopFlowContext> loopContexts = [ ];
    private int nextLocalSlot;

    private Binder(
        SourceText source,
        EnvironmentSchema environment,
        TypeSymbol expectedResultType
    )
    {
        this.source = source;
        this.environment = environment;
        this.expectedResultType = expectedResultType;
    }

    public static BindingResult Bind(
        SyntaxTree syntaxTree,
        EnvironmentSchema environment,
        TypeSymbol? expectedResultType = null
    )
    {
        if (syntaxTree is null)
        {
            throw new ArgumentNullException(nameof(syntaxTree));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        TypeSymbol resultType = expectedResultType ?? TypeSymbols.Void;

        if (
            syntaxTree.CompilationMode == CompilationMode.Expression &&
            resultType.Kind == TypeKind.Void &&
            expectedResultType is not null
        )
        {
            throw new ArgumentException(
                "Expression mode cannot have void as its expected result type.",
                nameof(expectedResultType)
            );
        }

        Binder binder = new (syntaxTree.Source, environment, resultType);
        BoundRoot root = syntaxTree.Root switch
        {
            ExpressionRootSyntax expressionRoot => binder.BindExpressionRoot(
                expressionRoot,
                expectedResultType
            ),
            ProgramRootSyntax programRoot => binder.BindProgramRoot(programRoot),
            _ => throw new InvalidOperationException("Unknown syntax root."),
        };
        DiagnosticCollection diagnostics = DiagnosticCollection.Create(
            syntaxTree.Diagnostics.Concat(binder.diagnostics)
        );

        return new BindingResult(root, diagnostics);
    }

    private BoundRoot BindExpressionRoot(
        ExpressionRootSyntax syntax,
        TypeSymbol? expectedType
    )
    {
        BoundExpression expression = BindExpression(syntax.Expression, expectedType);

        if (expression.Type.Kind == TypeKind.Void)
        {
            Report(
                DiagnosticCodes.InvalidVoidExpression,
                syntax.Expression.Span,
                "An expression compilation cannot return void."
            );
        }
        else if (expression.Type.Kind == TypeKind.Null && expectedType is null)
        {
            Report(
                DiagnosticCodes.CannotInferType,
                syntax.Expression.Span,
                "The result type of a null expression cannot be inferred."
            );
        }

        return new BoundRoot.Expression(syntax, expression);
    }

    private BoundRoot BindProgramRoot(ProgramRootSyntax syntax)
    {
        FlowState state = new ([ ]);
        List<BoundStatement> statements = [ ];

        foreach (StatementSyntax statementSyntax in syntax.Statements)
        {
            BindStatementWithReachability(statementSyntax, state, statements);
        }

        if (
            expectedResultType.Kind != TypeKind.Void &&
            state.CanCompleteNormally
        )
        {
            Report(
                DiagnosticCodes.NotAllPathsReturn,
                syntax.Span,
                $"Not all program paths return '{expectedResultType.DisplayName}'.",
                DiagnosticCategory.ControlFlow
            );
        }

        return new BoundRoot.Program(
            syntax,
            statements.AsReadOnly(),
            expectedResultType
        );
    }

    private void BindStatementWithReachability(
        StatementSyntax syntax,
        FlowState state,
        ICollection<BoundStatement> statements
    )
    {
        if (!state.CanCompleteNormally)
        {
            Report(
                DiagnosticCodes.UnreachableStatement,
                syntax.Span,
                "Statement is unreachable.",
                DiagnosticCategory.ControlFlow
            );
        }

        statements.Add(BindStatement(syntax, state));
    }

    private BoundStatement BindStatement(StatementSyntax syntax, FlowState state)
    {
        currentFlowState = state;

        return syntax switch
        {
            BlockStatementSyntax block => BindBlockStatement(block, state),
            VariableDeclarationStatementSyntax declaration =>
                BindVariableDeclaration(declaration, state),
            AssignmentStatementSyntax assignment => BindAssignment(assignment, state),
            RemovalStatementSyntax removal => BindRemoval(removal, state),
            ExpressionStatementSyntax expression => BindExpressionStatement(expression),
            IfStatementSyntax conditional => BindIfStatement(conditional, state),
            WhileStatementSyntax loop => BindWhileStatement(loop, state),
            ForStatementSyntax loop => BindForStatement(loop, state),
            BreakStatementSyntax loopExit => BindBreakStatement(loopExit, state),
            ContinueStatementSyntax iteration => BindContinueStatement(iteration, state),
            ReturnStatementSyntax result => BindReturnStatement(result, state),
            EmptyStatementSyntax empty => new BoundStatement.Empty(empty),
            _ => throw new InvalidOperationException("Unknown statement syntax."),
        };
    }

    private BoundStatement BindBlockStatement(
        BlockStatementSyntax syntax,
        FlowState state
    )
    {
        BindingScope parentScope = scope;
        BindingScope blockScope = new (parentScope);
        scope = blockScope;
        List<BoundStatement> statements = [ ];

        foreach (StatementSyntax statementSyntax in syntax.Statements)
        {
            BindStatementWithReachability(statementSyntax, state, statements);
        }

        foreach (LocalSymbol local in blockScope.Locals)
        {
            state.Assigned.Remove(local);
        }

        scope = parentScope;

        return new BoundStatement.Block(syntax, statements.AsReadOnly());
    }

    private BoundStatement BindVariableDeclaration(
        VariableDeclarationStatementSyntax syntax,
        FlowState state
    )
    {
        TypeSymbol? declaredType = syntax.Type is null
            ? null
            : BindType(syntax.Type);
        BoundExpression? initializer = syntax.Initializer is null
            ? null
            : BindExpression(syntax.Initializer, declaredType);
        TypeSymbol localType = declaredType ?? InferLocalType(syntax, initializer);
        string name = GetText(syntax.IdentifierToken);
        LocalSymbol local = new (name, localType, nextLocalSlot++);
        bool hasCurrentLocal = scope.ContainsLocal(name);
        bool hasVisibleLocal = scope.TryLookup(name, out _);
        bool hasVisibleGlobal = environment.TryGetGlobal(name, out _);

        if (hasVisibleLocal || hasVisibleGlobal)
        {
            Report(
                hasCurrentLocal
                    ? DiagnosticCodes.DuplicateLocal
                    : DiagnosticCodes.ShadowedVariable,
                syntax.IdentifierToken.Span,
                hasCurrentLocal
                    ? $"Local variable '{name}' is already declared in a visible scope."
                    : $"Local variable '{name}' cannot shadow another visible variable."
            );
        }

        if (!hasCurrentLocal)
        {
            scope.TryDeclare(local);
        }

        if (initializer is not null)
        {
            state.Assigned.Add(local);
        }

        return new BoundStatement.VariableDeclaration(syntax, local, initializer);
    }

    private TypeSymbol InferLocalType(
        VariableDeclarationStatementSyntax syntax,
        BoundExpression? initializer
    )
    {
        if (initializer is not (null or { Type.Kind: TypeKind.Null or TypeKind.Error or TypeKind.Void }))
        {
            return initializer.Type;
        }

        Report(
            DiagnosticCodes.CannotInferType,
            syntax.Span,
            "The local variable type cannot be inferred from its initializer."
        );
        return TypeSymbols.Error;
    }

    private BoundStatement BindAssignment(
        AssignmentStatementSyntax syntax,
        FlowState state
    )
    {
        BoundExpression target = BindAssignmentTarget(syntax.Target, state);
        BoundExpression value = BindExpression(syntax.Value, target.Type);

        if (target is BoundExpression.MemberAccess { IsArrayLength: true })
        {
            Report(
                DiagnosticCodes.ReadOnlyTarget,
                syntax.Target.Span,
                "The intrinsic array length property is read-only."
            );
        }

        if (target is BoundExpression.Local local && value.Type.Kind != TypeKind.Error)
        {
            state.Assigned.Add(local.Symbol);
        }

        return new BoundStatement.Assignment(syntax, target, value);
    }

    private BoundStatement BindRemoval(
        RemovalStatementSyntax syntax,
        FlowState state
    )
    {
        BoundExpression target = BindAssignmentTarget(syntax.Target, state);
        bool isRemovable = target switch
        {
            BoundExpression.MemberAccess member =>
                !member.IsArrayLength &&
                (member.IsDynamic || member.Property?.IsOptional == true),
            BoundExpression.ElementAccess element =>
                element.IsObjectAccess &&
                (element.IsDynamic || element.Property?.IsOptional == true),
            _ => false,
        };

        if (!isRemovable && target.Type.Kind != TypeKind.Error)
        {
            Report(
                DiagnosticCodes.PropertyNotRemovable,
                syntax.Target.Span,
                "The selected property is not removable."
            );
        }

        return new BoundStatement.Removal(syntax, target);
    }

    private BoundStatement BindExpressionStatement(ExpressionStatementSyntax syntax)
    {
        BoundExpression expression = BindExpression(syntax.Expression);

        return new BoundStatement.ExpressionStatement(syntax, expression);
    }

    private BoundStatement BindIfStatement(IfStatementSyntax syntax, FlowState state)
    {
        BoundExpression condition = BindExpression(syntax.Condition, TypeSymbols.Bool);
        FlowState thenState = state.Clone();
        BoundStatement thenStatement = BindEmbeddedStatement(
            syntax.ThenStatement,
            thenState
        );
        FlowState elseState = state.Clone();
        BoundStatement? elseStatement = syntax.ElseStatement is null
            ? null
            : BindEmbeddedStatement(syntax.ElseStatement, elseState);

        MergeBranches(state, thenState, elseState);

        return new BoundStatement.If(
            syntax,
            condition,
            thenStatement,
            elseStatement
        );
    }

    private BoundStatement BindWhileStatement(
        WhileStatementSyntax syntax,
        FlowState state
    )
    {
        bool wasReachable = state.CanCompleteNormally;
        BoundExpression condition = BindExpression(syntax.Condition, TypeSymbols.Bool);
        FlowState bodyState = state.Clone();
        LoopFlowContext loopContext = new ();
        loopContexts.Push(loopContext);
        BoundStatement body = BindEmbeddedStatement(syntax.Body, bodyState);
        loopContexts.Pop();

        if (wasReachable && IsConstantTrue(condition))
        {
            if (loopContext.BreakStates.Count == 0)
            {
                state.CanCompleteNormally = false;
            }
            else
            {
                ReplaceAssignedWithIntersection(state, loopContext.BreakStates);
            }
        }

        return new BoundStatement.While(syntax, condition, body);
    }

    private BoundStatement BindForStatement(
        ForStatementSyntax syntax,
        FlowState state
    )
    {
        bool wasReachable = state.CanCompleteNormally;
        BindingScope parentScope = scope;
        BindingScope forScope = new (parentScope);
        scope = forScope;
        BoundStatement? initializer = syntax.Initializer is null
            ? null
            : BindStatement(syntax.Initializer, state);
        BoundExpression? condition = syntax.Condition is null
            ? null
            : BindExpression(syntax.Condition, TypeSymbols.Bool);
        FlowState bodyState = state.Clone();
        LoopFlowContext loopContext = new ();
        loopContexts.Push(loopContext);
        BoundStatement body = BindEmbeddedStatement(syntax.Body, bodyState);
        List<FlowState> iteratorEntryStates = [ ];

        if (bodyState.CanCompleteNormally)
        {
            iteratorEntryStates.Add(bodyState);
        }

        iteratorEntryStates.AddRange(loopContext.ContinueStates);
        FlowState iteratorState;

        if (iteratorEntryStates.Count == 0)
        {
            iteratorState = state.Clone();
            iteratorState.CanCompleteNormally = false;
        }
        else
        {
            iteratorState = CreateIntersectionState(iteratorEntryStates);
        }

        BoundStatement? iterator = syntax.Iterator is null
            ? null
            : BindStatement(syntax.Iterator, iteratorState);
        loopContexts.Pop();

        if (wasReachable && (condition is null || IsConstantTrue(condition)))
        {
            if (loopContext.BreakStates.Count == 0)
            {
                state.CanCompleteNormally = false;
            }
            else
            {
                ReplaceAssignedWithIntersection(state, loopContext.BreakStates);
            }
        }

        foreach (LocalSymbol local in forScope.Locals)
        {
            state.Assigned.Remove(local);
        }

        scope = parentScope;

        return new BoundStatement.For(
            syntax,
            initializer,
            condition,
            iterator,
            body
        );
    }

    private BoundStatement BindEmbeddedStatement(
        StatementSyntax syntax,
        FlowState state
    )
    {
        if (syntax is VariableDeclarationStatementSyntax)
        {
            Report(
                DiagnosticCodes.InvalidEmbeddedDeclaration,
                syntax.Span,
                "A local variable declaration used as an embedded statement must be enclosed in a block."
            );
        }

        if (syntax is BlockStatementSyntax)
        {
            return BindStatement(syntax, state);
        }

        BindingScope parentScope = scope;
        BindingScope embeddedScope = new (parentScope);
        scope = embeddedScope;
        BoundStatement statement = BindStatement(syntax, state);

        foreach (LocalSymbol local in embeddedScope.Locals)
        {
            state.Assigned.Remove(local);
        }

        scope = parentScope;
        return statement;
    }

    private BoundStatement BindBreakStatement(
        BreakStatementSyntax syntax,
        FlowState state
    )
    {
        if (loopContexts.Count == 0)
        {
            Report(
                DiagnosticCodes.BreakOutsideLoop,
                syntax.Span,
                "A break statement is only valid inside a loop.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (state.CanCompleteNormally)
        {
            loopContexts.Peek().AddBreakState(state);
        }

        state.CanCompleteNormally = false;

        return new BoundStatement.Break(syntax);
    }

    private BoundStatement BindContinueStatement(
        ContinueStatementSyntax syntax,
        FlowState state
    )
    {
        if (loopContexts.Count == 0)
        {
            Report(
                DiagnosticCodes.ContinueOutsideLoop,
                syntax.Span,
                "A continue statement is only valid inside a loop.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (state.CanCompleteNormally)
        {
            loopContexts.Peek().AddContinueState(state);
        }

        state.CanCompleteNormally = false;

        return new BoundStatement.Continue(syntax);
    }

    private BoundStatement BindReturnStatement(
        ReturnStatementSyntax syntax,
        FlowState state
    )
    {
        BoundExpression? expression = syntax.Expression is null
            ? null
            : BindExpression(
                syntax.Expression,
                expectedResultType.Kind == TypeKind.Void
                    ? null
                    : expectedResultType
            );

        if (expectedResultType.Kind == TypeKind.Void && expression is not null)
        {
            Report(
                DiagnosticCodes.InvalidReturn,
                syntax.Span,
                "A void program cannot return a value.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (expectedResultType.Kind != TypeKind.Void && expression is null)
        {
            Report(
                DiagnosticCodes.InvalidReturn,
                syntax.Span,
                $"The program must return '{expectedResultType.DisplayName}'.",
                DiagnosticCategory.ControlFlow
            );
        }

        state.CanCompleteNormally = false;

        return new BoundStatement.Return(syntax, expression);
    }

    private BoundExpression BindExpression(
        ExpressionSyntax syntax,
        TypeSymbol? expectedType = null
    )
    {
        BoundExpression expression = syntax switch
        {
            MissingExpressionSyntax missing => new BoundExpression.Error(missing),
            LiteralExpressionSyntax literal => BindLiteralExpression(literal),
            NameExpressionSyntax name => BindNameExpression(name),
            ParenthesizedExpressionSyntax parenthesized =>
                BindExpression(parenthesized.Expression, expectedType),
            ArrayLiteralExpressionSyntax array => BindArrayExpression(array, expectedType),
            ObjectLiteralExpressionSyntax objectLiteral =>
                BindObjectExpression(objectLiteral, expectedType),
            UnaryExpressionSyntax unary => BindUnaryExpression(unary),
            BinaryExpressionSyntax binary => BindBinaryExpression(binary),
            ConversionExpressionSyntax conversion => BindConversionExpression(conversion),
            TypeTestExpressionSyntax typeTest => BindTypeTestExpression(typeTest),
            PropertyTestExpressionSyntax propertyTest =>
                BindPropertyTestExpression(propertyTest),
            ConditionalExpressionSyntax conditional =>
                BindConditionalExpression(conditional, expectedType),
            CallExpressionSyntax call => BindCallExpression(call),
            MemberAccessExpressionSyntax member => BindMemberAccess(member),
            ElementAccessExpressionSyntax element => BindElementAccess(element),
            _ => throw new InvalidOperationException("Unknown expression syntax."),
        };

        if (expectedType is null || expression.Type.Kind == TypeKind.Error)
        {
            return expression;
        }

        if (!TypeRelations.IsAssignable(expression.Type, expectedType))
        {
            ReportTypeMismatch(syntax.Span, expression.Type, expectedType);
            return expression;
        }

        return ConvertImplicit(expression, expectedType);
    }

    private BoundExpression BindLiteralExpression(LiteralExpressionSyntax syntax)
    {
        switch (syntax.LiteralToken.Kind)
        {
            case TokenKind.IntegerLiteral:
            {
                string text = GetSignedLiteralText(syntax);

                if (
                    !long.TryParse(
                        text,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out long value
                    )
                )
                {
                    Report(
                        DiagnosticCodes.InvalidIntegerLiteral,
                        syntax.Span,
                        $"Integer literal '{text}' is outside the supported range."
                    );

                    return new BoundExpression.Error(syntax);
                }

                return new BoundExpression.Literal(syntax, TypeSymbols.Int, value);
            }

            case TokenKind.NumberLiteral:
            {
                string text = GetSignedLiteralText(syntax);

                if (
                    !double.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value
                    ) ||
                    !double.IsFinite(value)
                )
                {
                    Report(
                        DiagnosticCodes.InvalidNumberLiteral,
                        syntax.Span,
                        $"Number literal '{text}' is outside the supported range."
                    );

                    return new BoundExpression.Error(syntax);
                }

                return new BoundExpression.Literal(syntax, TypeSymbols.Number, value);
            }

            case TokenKind.StringLiteral:
            {
                return new BoundExpression.Literal(
                    syntax,
                    TypeSymbols.String,
                    syntax.LiteralToken.Value ?? ""
                );
            }

            case TokenKind.TrueKeyword:
            {
                return new BoundExpression.Literal(syntax, TypeSymbols.Bool, true);
            }

            case TokenKind.FalseKeyword:
            {
                return new BoundExpression.Literal(syntax, TypeSymbols.Bool, false);
            }

            case TokenKind.NullKeyword:
            {
                return new BoundExpression.Literal(syntax, TypeSymbols.Null, null);
            }

            default:
            {
                return new BoundExpression.Error(syntax);
            }
        }
    }

    private BoundExpression BindNameExpression(NameExpressionSyntax syntax)
    {
        string name = GetText(syntax.IdentifierToken);

        if (scope.TryLookup(name, out LocalSymbol? local))
        {
            if (
                currentFlowState.CanCompleteNormally &&
                !currentFlowState.Assigned.Contains(local)
            )
            {
                Report(
                    DiagnosticCodes.UnassignedLocal,
                    syntax.Span,
                    $"Local variable '{name}' is used before it is definitely assigned.",
                    DiagnosticCategory.ControlFlow
                );
            }

            return new BoundExpression.Local(syntax, local);
        }

        if (environment.TryGetGlobal(name, out GlobalSymbol? global))
        {
            return new BoundExpression.Global(syntax, global);
        }

        Report(
            DiagnosticCodes.UndefinedName,
            syntax.Span,
            $"Variable '{name}' is not defined."
        );

        return new BoundExpression.Error(syntax);
    }

    private BoundExpression BindArrayExpression(
        ArrayLiteralExpressionSyntax syntax,
        TypeSymbol? expectedType
    )
    {
        ArrayTypeSymbol? expectedArray = GetNonNullable(expectedType) as ArrayTypeSymbol;
        List<BoundExpression> elements = [ ];
        TypeSymbol? elementType = null;
        bool hasInvalidElementType = false;

        foreach (ExpressionSyntax elementSyntax in syntax.Elements)
        {
            BoundExpression element = BindExpression(elementSyntax, expectedArray?.ElementType);
            elements.Add(element);

            if (element.Type.Kind == TypeKind.Error)
            {
                hasInvalidElementType = true;
                continue;
            }

            if (expectedArray is not null)
            {
                continue;
            }

            if (element.Type.Kind == TypeKind.Void)
            {
                Report(
                    DiagnosticCodes.InvalidVoidExpression,
                    elementSyntax.Span,
                    "A void expression cannot be used as an array element."
                );
                hasInvalidElementType = true;
                continue;
            }

            TypeSymbol? commonType = elementType is null
                ? element.Type
                : TypeRelations.GetCommonType(elementType, element.Type);

            if (commonType is null)
            {
                Report(
                    DiagnosticCodes.CannotInferType,
                    syntax.Span,
                    "Array elements do not have a common type."
                );
                hasInvalidElementType = true;
                continue;
            }

            elementType = commonType;
        }

        if (expectedArray is not null)
        {
            return new BoundExpression.Array(
                syntax,
                expectedArray,
                elements.AsReadOnly()
            );
        }

        if (
            elementType is null &&
            !hasInvalidElementType ||
            elementType?.Kind == TypeKind.Null
        )
        {
            Report(
                DiagnosticCodes.CannotInferType,
                syntax.Span,
                syntax.Elements.Count == 0
                    ? "The element type of an empty array literal cannot be inferred."
                    : "An array element type cannot be inferred exclusively from null values."
            );
            hasInvalidElementType = true;
        }

        ArrayTypeSymbol arrayType =
            elementType is null || hasInvalidElementType
                ? TypeSymbols.Array(TypeSymbols.Unknown)
                : TypeSymbols.Array(elementType);

        if (!hasInvalidElementType)
        {
            for (int index = 0; index < elements.Count; index++)
            {
                elements[index] = ConvertImplicit(elements[index], arrayType.ElementType);
            }
        }

        return new BoundExpression.Array(syntax, arrayType, elements.AsReadOnly());
    }

    private BoundExpression BindObjectExpression(
        ObjectLiteralExpressionSyntax syntax,
        TypeSymbol? expectedType
    )
    {
        ObjectTypeSymbol? expectedObject = GetNonNullable(expectedType) as ObjectTypeSymbol;
        List<BoundExpression.ObjectProperty> properties = [ ];
        List<ObjectPropertySymbol> propertySymbols = [ ];
        HashSet<string> names = new (StringComparer.Ordinal);

        if (expectedObject is not null && syntax.IsOpen != expectedObject.IsOpen)
        {
            Report(
                DiagnosticCodes.TypeMismatch,
                syntax.OpenBraceToken.Span,
                $"The object literal openness does not match '{expectedObject.DisplayName}'."
            );
        }

        foreach (ObjectPropertyInitializerSyntax propertySyntax in syntax.Properties)
        {
            string name = propertySyntax.NameToken.Kind == TokenKind.StringLiteral
                ? propertySyntax.NameToken.Value ?? ""
                : GetText(propertySyntax.NameToken);
            ObjectPropertySymbol? expectedProperty = null;

            _ = expectedObject?.TryGetProperty(name, out expectedProperty);

            TypeSymbol? expectedPropertyType = expectedProperty?.Type;

            if (
                expectedPropertyType is null &&
                expectedObject?.IsOpen == true
            )
            {
                expectedPropertyType = TypeSymbols.Unknown;
            }

            if (
                expectedObject is not null &&
                expectedProperty is null &&
                !expectedObject.IsOpen
            )
            {
                Report(
                    DiagnosticCodes.PropertyNotFound,
                    propertySyntax.NameToken.Span,
                    $"Property '{name}' is not declared by '{expectedObject.DisplayName}'."
                );
            }

            BoundExpression value = BindExpression(
                propertySyntax.Value,
                expectedPropertyType
            );
            bool isFirstDeclaration = names.Add(name);

            if (!isFirstDeclaration)
            {
                Report(
                    DiagnosticCodes.DuplicateObjectProperty,
                    propertySyntax.NameToken.Span,
                    $"Object property '{name}' is declared more than once."
                );
            }

            TypeSymbol propertyType = value.Type.Kind is
                TypeKind.Null or
                TypeKind.Void or
                TypeKind.Error
                ? TypeSymbols.Unknown
                : value.Type;

            if (value.Type.Kind == TypeKind.Null)
            {
                Report(
                    DiagnosticCodes.CannotInferType,
                    propertySyntax.Value.Span,
                    $"The type of object property '{name}' cannot be inferred from null."
                );
            }
            else if (value.Type.Kind == TypeKind.Void)
            {
                Report(
                    DiagnosticCodes.InvalidVoidExpression,
                    propertySyntax.Value.Span,
                    "A void expression cannot initialize an object property."
                );
            }

            properties.Add(new BoundExpression.ObjectProperty(name, value));

            if (isFirstDeclaration)
            {
                propertySymbols.Add(
                    new ObjectPropertySymbol(
                        name,
                        propertyType,
                        propertySyntax.IsOptional
                    )
                );
            }
        }

        if (expectedObject is not null)
        {
            foreach (ObjectPropertySymbol property in expectedObject.Properties)
            {
                if (!property.IsOptional && !names.Contains(property.Name))
                {
                    Report(
                        DiagnosticCodes.MissingObjectProperty,
                        syntax.Span,
                        $"Required property '{property.Name}' is missing from the object literal."
                    );
                }
            }
        }

        ObjectTypeSymbol objectType = expectedObject ??
            ObjectTypeSymbol.CreateAnonymous(syntax.IsOpen, propertySymbols);

        return new BoundExpression.Object(syntax, objectType, properties.AsReadOnly());
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax syntax)
    {
        BoundExpression operand = BindExpression(syntax.Operand);
        TypeSymbol operandType = GetNonNullable(operand.Type);
        TypeSymbol? resultType = syntax.OperatorToken.Kind switch
        {
            TokenKind.Plus or TokenKind.Minus
                when IsNumeric(operandType) => operandType,
            TokenKind.Bang
                when operandType.Kind == TypeKind.Bool => TypeSymbols.Bool,
            TokenKind.Tilde
                when operandType.Kind == TypeKind.Int => TypeSymbols.Int,
            _ => null,
        };

        if (operand.Type.Kind == TypeKind.Error)
        {
            return new BoundExpression.Error(syntax);
        }

        if (resultType is null)
        {
            ReportOperatorNotDefined(
                syntax.OperatorToken,
                operand.Type
            );
            return new BoundExpression.Error(syntax);
        }

        operand = ConvertRequiredOperand(operand, resultType);

        return new BoundExpression.Unary(
            syntax,
            resultType,
            syntax.OperatorToken.Kind,
            operand
        );
    }

    private BoundExpression BindBinaryExpression(BinaryExpressionSyntax syntax)
    {
        BoundExpression left = BindExpression(syntax.Left);
        BoundExpression right = BindExpression(syntax.Right);

        if (left.Type.Kind == TypeKind.Error || right.Type.Kind == TypeKind.Error)
        {
            return new BoundExpression.Error(syntax);
        }

        TypeSymbol leftType = GetNonNullable(left.Type);
        TypeSymbol rightType = GetNonNullable(right.Type);
        TypeSymbol? resultType = GetBinaryResultType(
            syntax.OperatorToken.Kind,
            leftType,
            rightType
        );

        if (resultType is null)
        {
            ReportOperatorNotDefined(
                syntax.OperatorToken,
                left.Type,
                right.Type
            );
            return new BoundExpression.Error(syntax);
        }

        ApplyBinaryConversions(
            syntax.OperatorToken.Kind,
            resultType,
            ref left,
            ref right
        );

        return new BoundExpression.Binary(
            syntax,
            resultType,
            left,
            syntax.OperatorToken.Kind,
            right
        );
    }

    private static TypeSymbol? GetBinaryResultType(
        TokenKind operatorKind,
        TypeSymbol left,
        TypeSymbol right
    )
    {
        if (
            operatorKind == TokenKind.Plus &&
            IsStringConcatenation(left, right)
        )
        {
            return TypeSymbols.String;
        }

        if (
            operatorKind is
                TokenKind.Plus or
                TokenKind.Minus or
                TokenKind.Asterisk or
                TokenKind.Slash or
                TokenKind.Percent &&
            IsNumeric(left) &&
            IsNumeric(right)
        )
        {
            return left.Kind == TypeKind.Number || right.Kind == TypeKind.Number
                ? TypeSymbols.Number
                : TypeSymbols.Int;
        }

        if (
            operatorKind is
                TokenKind.LeftShift or
                TokenKind.RightShift or
                TokenKind.Ampersand or
                TokenKind.Caret or
                TokenKind.Pipe &&
            left.Kind == TypeKind.Int &&
            right.Kind == TypeKind.Int
        )
        {
            return TypeSymbols.Int;
        }

        if (
            operatorKind is
                TokenKind.LessThan or
                TokenKind.LessThanOrEqual or
                TokenKind.GreaterThan or
                TokenKind.GreaterThanOrEqual &&
            (
                (IsNumeric(left) && IsNumeric(right)) ||
                (left.Kind == TypeKind.String && right.Kind == TypeKind.String)
            )
        )
        {
            return TypeSymbols.Bool;
        }

        if (
            operatorKind is
                TokenKind.EqualEqual or
                TokenKind.BangEqual or
                TokenKind.EqualEqualEqual or
                TokenKind.BangEqualEqual &&
            left.Kind != TypeKind.Void &&
            right.Kind != TypeKind.Void
        )
        {
            return TypeSymbols.Bool;
        }

        if (
            operatorKind is TokenKind.AmpersandAmpersand or TokenKind.PipePipe &&
            left.Kind == TypeKind.Bool &&
            right.Kind == TypeKind.Bool
        )
        {
            return TypeSymbols.Bool;
        }

        return null;
    }

    private BoundExpression BindConversionExpression(
        ConversionExpressionSyntax syntax
    )
    {
        BoundExpression expression = BindExpression(syntax.Expression);
        TypeSymbol targetType = BindType(syntax.Type);
        ConversionKind conversion = TypeRelations.ClassifyConversion(
            expression.Type,
            targetType
        );

        if (conversion == ConversionKind.None)
        {
            Report(
                DiagnosticCodes.InvalidConversion,
                syntax.Span,
                $"Type '{expression.Type.DisplayName}' cannot be converted to '{targetType.DisplayName}'."
            );
            return new BoundExpression.Error(syntax);
        }

        return new BoundExpression.Conversion(
            syntax,
            targetType,
            expression,
            conversion
        );
    }

    private BoundExpression BindTypeTestExpression(TypeTestExpressionSyntax syntax)
    {
        BoundExpression expression = BindExpression(syntax.Expression);
        TypeSymbol testedType = BindType(syntax.Type);

        if (expression.Type.Kind == TypeKind.Void)
        {
            Report(
                DiagnosticCodes.InvalidVoidExpression,
                syntax.Expression.Span,
                "A void expression cannot be tested with 'is'."
            );
        }

        return new BoundExpression.TypeTest(syntax, expression, testedType);
    }

    private BoundExpression BindPropertyTestExpression(
        PropertyTestExpressionSyntax syntax
    )
    {
        BoundExpression target = BindExpression(syntax.Target);
        BoundExpression key = BindExpression(syntax.Key, TypeSymbols.String);
        TypeSymbol targetType = GetNonNullable(target.Type);

        if (
            target.Type.Kind != TypeKind.Error &&
            targetType.Kind is not TypeKind.Object and not TypeKind.StructuredObject
        )
        {
            Report(
                DiagnosticCodes.OperatorNotDefined,
                syntax.HasKeyword.Span,
                $"Operator 'has' is not defined for '{target.Type.DisplayName}'."
            );
        }

        return new BoundExpression.PropertyTest(syntax, target, key);
    }

    private BoundExpression BindConditionalExpression(
        ConditionalExpressionSyntax syntax,
        TypeSymbol? expectedType
    )
    {
        BoundExpression condition = BindExpression(syntax.Condition, TypeSymbols.Bool);
        BoundExpression whenTrue = BindExpression(syntax.WhenTrue, expectedType);
        BoundExpression whenFalse = BindExpression(syntax.WhenFalse, expectedType);
        TypeSymbol? resultType = expectedType ??
            TypeRelations.GetCommonType(whenTrue.Type, whenFalse.Type);

        if (resultType is null)
        {
            Report(
                DiagnosticCodes.TypeMismatch,
                syntax.Span,
                $"Conditional branches '{whenTrue.Type.DisplayName}' and '{whenFalse.Type.DisplayName}' have no common type."
            );
            return new BoundExpression.Error(syntax);
        }

        whenTrue = ConvertImplicit(whenTrue, resultType);
        whenFalse = ConvertImplicit(whenFalse, resultType);

        return new BoundExpression.Conditional(
            syntax,
            resultType,
            condition,
            whenTrue,
            whenFalse
        );
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax syntax)
    {
        FunctionSymbol? function = null;

        if (syntax.Target is NameExpressionSyntax nameSyntax)
        {
            string name = GetText(nameSyntax.IdentifierToken);

            if (!environment.TryGetFunction(name, out function))
            {
                Report(
                    DiagnosticCodes.UndefinedFunction,
                    nameSyntax.Span,
                    $"Function '{name}' is not defined."
                );
            }
        }
        else
        {
            BindExpression(syntax.Target);
            Report(
                DiagnosticCodes.InvalidCallTarget,
                syntax.Target.Span,
                "Only a provider function name can be called."
            );
        }

        List<BoundExpression> arguments = [ ];

        for (int index = 0; index < syntax.Arguments.Count; index++)
        {
            TypeSymbol? parameterType =
                function is not null && index < function.Parameters.Count
                    ? function.Parameters[index].Type
                    : null;
            arguments.Add(BindExpression(syntax.Arguments[index], parameterType));
        }

        if (function is null)
        {
            return new BoundExpression.Error(syntax);
        }

        if (syntax.Arguments.Count != function.Parameters.Count)
        {
            Report(
                DiagnosticCodes.ArgumentCountMismatch,
                syntax.Span,
                $"Function '{function.Name}' expects {function.Parameters.Count} arguments but received {syntax.Arguments.Count}."
            );
        }

        return new BoundExpression.Call(syntax, function, arguments.AsReadOnly());
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax syntax)
    {
        BoundExpression target = BindExpression(syntax.Target);
        string name = GetText(syntax.NameToken);
        TypeSymbol targetType = GetNonNullable(target.Type);

        if (targetType is ArrayTypeSymbol && name == "length")
        {
            TypeSymbol resultType = ShouldOptionalAccessReturnNullable(
                syntax.IsOptional,
                target.Type,
                false
            )
                ? TypeSymbols.Nullable(TypeSymbols.Int)
                : TypeSymbols.Int;

            return new BoundExpression.MemberAccess(
                syntax,
                resultType,
                target,
                name,
                null,
                false,
                syntax.IsOptional,
                true
            );
        }

        if (targetType is ObjectTypeSymbol objectType)
        {
            if (objectType.TryGetProperty(name, out ObjectPropertySymbol? property))
            {
                TypeSymbol resultType = ShouldOptionalAccessReturnNullable(
                    syntax.IsOptional,
                    target.Type,
                    property.IsOptional
                )
                    ? MakeNullable(property.Type)
                    : property.Type;

                return new BoundExpression.MemberAccess(
                    syntax,
                    resultType,
                    target,
                    name,
                    property,
                    false,
                    syntax.IsOptional,
                    false
                );
            }

            if (objectType.IsOpen)
            {
                return new BoundExpression.MemberAccess(
                    syntax,
                    syntax.IsOptional
                        ? TypeSymbols.Nullable(TypeSymbols.Unknown)
                        : TypeSymbols.Unknown,
                    target,
                    name,
                    null,
                    true,
                    syntax.IsOptional,
                    false
                );
            }
        }
        else if (targetType.Kind == TypeKind.Object)
        {
            return new BoundExpression.MemberAccess(
                syntax,
                syntax.IsOptional
                    ? TypeSymbols.Nullable(TypeSymbols.Unknown)
                    : TypeSymbols.Unknown,
                target,
                name,
                null,
                true,
                syntax.IsOptional,
                false
            );
        }

        if (target.Type.Kind != TypeKind.Error)
        {
            Report(
                DiagnosticCodes.PropertyNotFound,
                syntax.NameToken.Span,
                $"Property '{name}' is not available on '{target.Type.DisplayName}'."
            );
        }

        return new BoundExpression.Error(syntax);
    }

    private BoundExpression BindElementAccess(ElementAccessExpressionSyntax syntax)
    {
        BoundExpression target = BindExpression(syntax.Target);
        TypeSymbol targetType = GetNonNullable(target.Type);

        if (targetType is ArrayTypeSymbol arrayType)
        {
            BoundExpression index = BindExpression(syntax.Index, TypeSymbols.Int);
            TypeSymbol resultType =
                syntax.IsOptional && target.Type is NullableTypeSymbol
                    ? MakeNullable(arrayType.ElementType)
                    : arrayType.ElementType;

            return new BoundExpression.ElementAccess(
                syntax,
                resultType,
                target,
                index,
                null,
                false,
                false,
                syntax.IsOptional
            );
        }

        if (targetType is ObjectTypeSymbol objectType)
        {
            BoundExpression index = BindExpression(syntax.Index, TypeSymbols.String);
            ObjectPropertySymbol? property = TryResolveLiteralProperty(index, objectType);
            bool isDynamic = property is null && objectType.IsOpen;

            if (property is null && !isDynamic)
            {
                Report(
                    DiagnosticCodes.PropertyNotFound,
                    syntax.Index.Span,
                    $"The selected property is not available on '{target.Type.DisplayName}'."
                );
                return new BoundExpression.Error(syntax);
            }

            TypeSymbol propertyType = property?.Type ?? TypeSymbols.Unknown;
            TypeSymbol resultType = ShouldOptionalAccessReturnNullable(
                syntax.IsOptional,
                target.Type,
                property?.IsOptional == true || isDynamic
            )
                ? MakeNullable(propertyType)
                : propertyType;

            return new BoundExpression.ElementAccess(
                syntax,
                resultType,
                target,
                index,
                property,
                true,
                isDynamic,
                syntax.IsOptional
            );
        }

        if (targetType.Kind == TypeKind.Object)
        {
            BoundExpression index = BindExpression(syntax.Index, TypeSymbols.String);

            return new BoundExpression.ElementAccess(
                syntax,
                syntax.IsOptional
                    ? TypeSymbols.Nullable(TypeSymbols.Unknown)
                    : TypeSymbols.Unknown,
                target,
                index,
                null,
                true,
                true,
                syntax.IsOptional
            );
        }

        BindExpression(syntax.Index);

        if (target.Type.Kind != TypeKind.Error)
        {
            Report(
                DiagnosticCodes.InvalidIndex,
                syntax.Span,
                $"Type '{target.Type.DisplayName}' cannot be indexed."
            );
        }

        return new BoundExpression.Error(syntax);
    }

    private BoundExpression BindAssignmentTarget(
        ExpressionSyntax syntax,
        FlowState state
    )
    {
        currentFlowState = state;

        if (syntax is NameExpressionSyntax nameSyntax)
        {
            string name = GetText(nameSyntax.IdentifierToken);

            if (scope.TryLookup(name, out LocalSymbol? local))
            {
                return new BoundExpression.Local(nameSyntax, local);
            }

            if (environment.TryGetGlobal(name, out GlobalSymbol? global))
            {
                Report(
                    DiagnosticCodes.CannotAssignGlobal,
                    syntax.Span,
                    $"Global variable '{name}' cannot be assigned."
                );
                return new BoundExpression.Global(nameSyntax, global);
            }

            Report(
                DiagnosticCodes.UndefinedName,
                syntax.Span,
                $"Variable '{name}' is not defined."
            );
            return new BoundExpression.Error(syntax);
        }

        if (syntax is MemberAccessExpressionSyntax memberSyntax)
        {
            return BindMemberAccess(memberSyntax);
        }

        if (syntax is ElementAccessExpressionSyntax elementSyntax)
        {
            return BindElementAccess(elementSyntax);
        }

        BindExpression(syntax);

        return new BoundExpression.Error(syntax);
    }

    private TypeSymbol BindType(TypeSyntax syntax)
    {
        string name = GetText(syntax.NameToken);
        TypeSymbol type = syntax.NameToken.Kind switch
        {
            TokenKind.BoolKeyword => TypeSymbols.Bool,
            TokenKind.IntKeyword => TypeSymbols.Int,
            TokenKind.NumberKeyword => TypeSymbols.Number,
            TokenKind.StringKeyword => TypeSymbols.String,
            TokenKind.UnknownKeyword => TypeSymbols.Unknown,
            TokenKind.ObjectKeyword => TypeSymbols.Object,
            TokenKind.Identifier
                when environment.TryGetType(name, out ObjectTypeSymbol? objectType) =>
                objectType,
            _ => TypeSymbols.Error,
        };

        if (type.Kind == TypeKind.Error)
        {
            Report(
                DiagnosticCodes.UndefinedType,
                syntax.NameToken.Span,
                $"Type '{name}' is not defined."
            );
            return type;
        }

        for (int index = 0; index < syntax.SuffixTokens.Count; index++)
        {
            SyntaxToken suffix = syntax.SuffixTokens[index];

            if (suffix.Kind == TokenKind.Question)
            {
                if (type is not NullableTypeSymbol)
                {
                    type = TypeSymbols.Nullable(type);
                }
            }
            else if (
                suffix.Kind == TokenKind.OpenBracket &&
                index + 1 < syntax.SuffixTokens.Count &&
                syntax.SuffixTokens[index + 1].Kind == TokenKind.CloseBracket
            )
            {
                type = TypeSymbols.Array(type);
                index++;
            }
        }

        return type;
    }

    private static ObjectPropertySymbol? TryResolveLiteralProperty(
        BoundExpression index,
        ObjectTypeSymbol objectType
    )
    {
        if (
            index is BoundExpression.Literal
            {
                Type.Kind: TypeKind.String,
                Value: string name,
            } &&
            objectType.TryGetProperty(name, out ObjectPropertySymbol? property)
        )
        {
            return property;
        }

        return null;
    }

    private void MergeBranches(
        FlowState target,
        FlowState thenState,
        FlowState elseState
    )
    {
        target.Assigned.Clear();

        if (thenState.CanCompleteNormally && elseState.CanCompleteNormally)
        {
            target.Assigned.UnionWith(thenState.Assigned);
            target.Assigned.IntersectWith(elseState.Assigned);
        }
        else if (thenState.CanCompleteNormally)
        {
            target.Assigned.UnionWith(thenState.Assigned);
        }
        else if (elseState.CanCompleteNormally)
        {
            target.Assigned.UnionWith(elseState.Assigned);
        }

        target.CanCompleteNormally =
            thenState.CanCompleteNormally || elseState.CanCompleteNormally;
        currentFlowState = target;
    }

    private static FlowState CreateIntersectionState(
        IReadOnlyCollection<FlowState> states
    )
    {
        FlowState first = states.First();
        FlowState result = first.Clone();

        foreach (FlowState state in states.Skip(1))
        {
            result.Assigned.IntersectWith(state.Assigned);
        }

        result.CanCompleteNormally = true;
        return result;
    }

    private static void ReplaceAssignedWithIntersection(
        FlowState target,
        IReadOnlyCollection<FlowState> states
    )
    {
        FlowState intersection = CreateIntersectionState(states);
        target.Assigned.Clear();
        target.Assigned.UnionWith(intersection.Assigned);
        target.CanCompleteNormally = true;
    }

    private static bool IsConstantTrue(BoundExpression expression)
    {
        return expression is BoundExpression.Literal
        {
            Type.Kind: TypeKind.Bool,
            Value: true,
        };
    }

    private static void ApplyBinaryConversions(
        TokenKind operatorKind,
        TypeSymbol resultType,
        ref BoundExpression left,
        ref BoundExpression right
    )
    {
        TypeSymbol leftType = GetNonNullable(left.Type);
        TypeSymbol rightType = GetNonNullable(right.Type);
        bool isArithmeticOrRelational =
            operatorKind is
                TokenKind.Plus or
                TokenKind.Minus or
                TokenKind.Asterisk or
                TokenKind.Slash or
                TokenKind.Percent or
                TokenKind.LessThan or
                TokenKind.LessThanOrEqual or
                TokenKind.GreaterThan or
                TokenKind.GreaterThanOrEqual &&
            IsNumeric(leftType) &&
            IsNumeric(rightType);

        if (isArithmeticOrRelational)
        {
            TypeSymbol operandType =
                leftType.Kind == TypeKind.Number || rightType.Kind == TypeKind.Number
                    ? TypeSymbols.Number
                    : TypeSymbols.Int;
            left = ConvertRequiredOperand(left, operandType);
            right = ConvertRequiredOperand(right, operandType);
            return;
        }

        if (operatorKind == TokenKind.Plus && resultType.Kind == TypeKind.String)
        {
            left = ConvertImplicit(left, TypeSymbols.String, true);
            right = ConvertImplicit(right, TypeSymbols.String, true);
            return;
        }

        if (
            operatorKind is
            TokenKind.LeftShift or
            TokenKind.RightShift or
            TokenKind.Ampersand or
            TokenKind.Caret or
            TokenKind.Pipe
        )
        {
            left = ConvertRequiredOperand(left, TypeSymbols.Int);
            right = ConvertRequiredOperand(right, TypeSymbols.Int);
            return;
        }

        if (operatorKind is TokenKind.AmpersandAmpersand or TokenKind.PipePipe)
        {
            left = ConvertRequiredOperand(left, TypeSymbols.Bool);
            right = ConvertRequiredOperand(right, TypeSymbols.Bool);
            return;
        }

        if (
            operatorKind is
                TokenKind.LessThan or
                TokenKind.LessThanOrEqual or
                TokenKind.GreaterThan or
                TokenKind.GreaterThanOrEqual &&
            leftType.Kind == TypeKind.String &&
            rightType.Kind == TypeKind.String
        )
        {
            left = ConvertRequiredOperand(left, TypeSymbols.String);
            right = ConvertRequiredOperand(right, TypeSymbols.String);
            return;
        }
    }

    private static BoundExpression ConvertRequiredOperand(
        BoundExpression expression,
        TypeSymbol targetType
    )
    {
        if (TypeRelations.AreEquivalent(expression.Type, targetType))
        {
            return expression;
        }

        ConversionKind conversionKind = TypeRelations.ClassifyConversion(
            expression.Type,
            targetType
        );

        return conversionKind == ConversionKind.None
            ? expression
            : new BoundExpression.Conversion(
                expression.Syntax,
                targetType,
                expression,
                conversionKind
            );
    }

    private static BoundExpression ConvertImplicit(
        BoundExpression expression,
        TypeSymbol targetType,
        bool force = false
    )
    {
        if (TypeRelations.AreEquivalent(expression.Type, targetType))
        {
            return expression;
        }

        ConversionKind conversionKind = force
            ? ConversionKind.Implicit
            : TypeRelations.ClassifyConversion(expression.Type, targetType);

        return conversionKind is ConversionKind.Identity or ConversionKind.Implicit
            ? new BoundExpression.Conversion(
                expression.Syntax,
                targetType,
                expression,
                ConversionKind.Implicit
            )
            : expression;
    }

    private static bool ShouldOptionalAccessReturnNullable(
        bool isOptional,
        TypeSymbol targetType,
        bool canBeAbsent
    )
    {
        return isOptional &&
            (targetType is NullableTypeSymbol || canBeAbsent);
    }

    private static TypeSymbol MakeNullable(TypeSymbol type)
    {
        return type is NullableTypeSymbol
            ? type
            : TypeSymbols.Nullable(type);
    }

    private static TypeSymbol GetNonNullable(TypeSymbol? type)
    {
        return type is NullableTypeSymbol nullable
            ? nullable.UnderlyingType
            : type ?? TypeSymbols.Error;
    }

    private static bool IsNumeric(TypeSymbol type)
    {
        return type.Kind is TypeKind.Int or TypeKind.Number;
    }

    private static bool IsStringConcatenation(TypeSymbol left, TypeSymbol right)
    {
        return left.Kind == TypeKind.String && IsStringConvertible(right) ||
            right.Kind == TypeKind.String && IsStringConvertible(left);
    }

    private static bool IsStringConvertible(TypeSymbol type)
    {
        return type.Kind is
            TypeKind.Bool or
            TypeKind.Int or
            TypeKind.Number or
            TypeKind.String or
            TypeKind.Null;
    }

    private string GetSignedLiteralText(LiteralExpressionSyntax syntax)
    {
        string sign = syntax.SignToken?.Kind switch
        {
            TokenKind.Plus => "+",
            TokenKind.Minus => "-",
            _ => "",
        };

        return $"{sign}{GetText(syntax.LiteralToken)}";
    }

    private string GetText(SyntaxToken token)
    {
        return token.Value ?? source.GetText(token.Span);
    }

    private void ReportTypeMismatch(
        TextSpan span,
        TypeSymbol actual,
        TypeSymbol expected
    )
    {
        Report(
            DiagnosticCodes.TypeMismatch,
            span,
            $"Type '{actual.DisplayName}' is not assignable to '{expected.DisplayName}'."
        );
    }

    private void ReportOperatorNotDefined(
        SyntaxToken operatorToken,
        params TypeSymbol[] operandTypes
    )
    {
        string operands = string.Join(
            " and ",
            operandTypes.Select(static type => $"'{type.DisplayName}'")
        );
        Report(
            DiagnosticCodes.OperatorNotDefined,
            operatorToken.Span,
            $"Operator '{GetText(operatorToken)}' is not defined for {operands}."
        );
    }

    private void Report(
        string code,
        TextSpan span,
        string message,
        DiagnosticCategory category = DiagnosticCategory.Type
    )
    {
        diagnostics.Add(
            new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                category,
                span,
                message
            )
        );
    }
}
