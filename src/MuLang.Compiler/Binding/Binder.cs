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
    private readonly LanguageProfile profile;
    private readonly TypeSymbol expectedResultType;
    private readonly IDictionary<string, UserFunctionSymbol> userFunctions;
    private readonly IDictionary<string, ISet<string>> userFunctionCalls;
    private readonly ICollection<Diagnostic> diagnostics;
    private readonly ISet<(string Code, int Start, int Length)> featureDiagnostics;
    private readonly ISet<(string Code, int Start, int Length)> warningDiagnostics;
    private readonly ISet<(int Start, int Length)> readOnlyAssignmentDiagnostics;
    private readonly string returnContext;
    private readonly string? currentFunctionId;
    private BindingScope scope = new (null);
    private FlowState currentFlowState = new ([ ]);
    private readonly Stack<LoopFlowContext> loopContexts = [ ];
    private readonly Stack<int> finallyLoopDepths = [ ];
    private LocalSymbol? currentCatchError;
    private int loopDepth;
    private int nextLocalSlot;

    private Binder(
        SourceText source,
        EnvironmentSchema environment,
        LanguageProfile profile,
        TypeSymbol expectedResultType,
        IDictionary<string, UserFunctionSymbol>? userFunctions = null,
        IDictionary<string, ISet<string>>? userFunctionCalls = null,
        ICollection<Diagnostic>? diagnostics = null,
        ISet<(string Code, int Start, int Length)>? featureDiagnostics = null,
        ISet<(string Code, int Start, int Length)>? warningDiagnostics = null,
        ISet<(int Start, int Length)>? readOnlyAssignmentDiagnostics = null,
        string? currentFunctionId = null,
        string returnContext = "program"
    )
    {
        this.source = source;
        this.environment = environment;
        this.profile = profile;
        this.expectedResultType = expectedResultType;
        this.userFunctions = userFunctions ??
            new Dictionary<string, UserFunctionSymbol>(StringComparer.Ordinal);
        this.userFunctionCalls = userFunctionCalls ??
            new Dictionary<string, ISet<string>>(StringComparer.Ordinal);
        this.diagnostics = diagnostics ?? [ ];
        this.featureDiagnostics = featureDiagnostics ??
            new HashSet<(string Code, int Start, int Length)>();
        this.warningDiagnostics = warningDiagnostics ??
            new HashSet<(string Code, int Start, int Length)>();
        this.readOnlyAssignmentDiagnostics = readOnlyAssignmentDiagnostics ??
            new HashSet<(int Start, int Length)>();
        this.currentFunctionId = currentFunctionId;
        this.returnContext = returnContext;
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

        ICollection<Diagnostic> reportedDiagnostics = [ ];
        ISet<(string Code, int Start, int Length)> featureDiagnostics =
            new HashSet<(string Code, int Start, int Length)>(
                syntaxTree.Diagnostics
                    .Where(
                        static diagnostic => diagnostic.Code.StartsWith(
                            "MUL7",
                            StringComparison.Ordinal
                        )
                    )
                    .Select(
                        static diagnostic => (
                            diagnostic.Code,
                            diagnostic.Span.Start,
                            diagnostic.Span.Length
                        )
                    )
            );
        Binder binder = new (
            syntaxTree.Source,
            environment,
            syntaxTree.LanguageProfile,
            resultType,
            diagnostics: reportedDiagnostics,
            featureDiagnostics: featureDiagnostics
        );
        binder.ValidateEnvironmentCompatibility();
        BoundRoot root = syntaxTree.Root switch
        {
            ExpressionRootSyntax expressionRoot => binder.BindExpressionRoot(
                expressionRoot,
                expectedResultType
            ),
            ProgramRootSyntax programRoot => binder.BindProgramRoot(programRoot),
            _ => throw new InvalidOperationException("Unknown syntax root."),
        };
        DiagnosticCollection allDiagnostics = DiagnosticCollection.Create(
            syntaxTree.Diagnostics.Concat(reportedDiagnostics)
        );

        return new BindingResult(
            root,
            allDiagnostics,
            syntaxTree.CompilationMode,
            syntaxTree.LanguageProfile.Fingerprint
        );
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
        IReadOnlyList<UserFunctionSymbol> functionSymbols =
            DeclareUserFunctions(syntax.Functions);
        IList<BoundFunction> functions = [ ];

        foreach (UserFunctionSymbol function in functionSymbols)
        {
            functions.Add(BindFunction(function));
        }

        if (
            profile is
            {
                UserDefinedFunctions: UserDefinedFunctionsFeature.Enabled,
                Recursion: RecursionFeature.Disabled,
            }
        )
        {
            ReportRecursiveFunctions(functionSymbols);
        }

        FlowState state = new ([ ]);
        IList<BoundStatement> statements = [ ];

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
                syntax.EndOfFileToken.Span,
                $"Not all program paths return '{expectedResultType.DisplayName}'.",
                DiagnosticCategory.ControlFlow
            );
        }

        return new BoundRoot.Program(
            syntax,
            functions.AsReadOnly(),
            statements.AsReadOnly(),
            expectedResultType
        );
    }

    private IReadOnlyList<UserFunctionSymbol> DeclareUserFunctions(
        IReadOnlyList<FunctionDeclarationSyntax> declarations
    )
    {
        IList<UserFunctionSymbol> symbols = [ ];

        for (int ordinal = 0; ordinal < declarations.Count; ordinal++)
        {
            FunctionDeclarationSyntax declaration = declarations[ordinal];
            if (
                profile.UserDefinedFunctions ==
                UserDefinedFunctionsFeature.Disabled
            )
            {
                ReportFeature(
                    DiagnosticCodes.DisabledUserDefinedFunctions,
                    declaration.FuncKeyword.Span,
                    "User-defined functions are disabled by the language profile."
                );
            }

            string name = GetText(declaration.IdentifierToken);
            IList<UserParameterSymbol> parameters = [ ];
            ISet<string> parameterNames = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < declaration.Parameters.Count; index++)
            {
                ParameterSyntax parameter = declaration.Parameters[index];
                string parameterName = GetText(parameter.IdentifierToken);
                TypeSymbol parameterType = BindType(parameter.Type);

                if (parameterType.Kind == TypeKind.Void)
                {
                    Report(
                        DiagnosticCodes.InvalidParameterType,
                        parameter.Type.Span,
                        "A function parameter cannot have type 'void'."
                    );
                    parameterType = TypeSymbols.ErrorRecovery;
                }

                if (!parameterNames.Add(parameterName))
                {
                    Report(
                        DiagnosticCodes.DuplicateParameter,
                        parameter.IdentifierToken.Span,
                        $"Parameter '{parameterName}' is declared more than once."
                    );
                }

                if (
                    environment.TryGetGlobal(parameterName, out _) &&
                    !profile.Shadowing.HasFlag(ShadowingPolicy.Globals)
                )
                {
                    Report(
                        DiagnosticCodes.ShadowedVariable,
                        parameter.IdentifierToken.Span,
                        $"Parameter '{parameterName}' cannot shadow a global variable."
                    );
                }

                parameters.Add(new UserParameterSymbol(parameterName, parameterType, index));
            }

            TypeSymbol returnType = BindType(declaration.ReturnType);

            if (returnType.Kind is TypeKind.Null or TypeKind.ErrorRecovery)
            {
                Report(
                    DiagnosticCodes.InvalidReturnType,
                    declaration.ReturnType.Span,
                    $"Type '{returnType.DisplayName}' cannot be used as a function return type."
                );
            }

            UserFunctionSymbol symbol = new (
                $"user:{ordinal}",
                name,
                parameters.AsReadOnly(),
                returnType,
                declaration,
                ordinal
            );
            symbols.Add(symbol);

            if (environment.TryGetFunction(name, out _))
            {
                Report(
                    DiagnosticCodes.FunctionConflict,
                    declaration.IdentifierToken.Span,
                    $"User function '{name}' conflicts with a provider function."
                );
            }

            if (!userFunctions.TryAdd(name, symbol))
            {
                Report(
                    DiagnosticCodes.DuplicateFunction,
                    declaration.IdentifierToken.Span,
                    $"User function '{name}' is declared more than once."
                );
            }
        }

        return symbols.AsReadOnly();
    }

    private BoundFunction BindFunction(UserFunctionSymbol function)
    {
        Binder functionBinder = new (
            source,
            environment,
            profile,
            function.ReturnType,
            userFunctions,
            userFunctionCalls,
            diagnostics,
            featureDiagnostics,
            warningDiagnostics,
            readOnlyAssignmentDiagnostics,
            function.Id,
            $"function '{function.Name}'"
        );
        userFunctionCalls.TryAdd(
            function.Id,
            new HashSet<string>(StringComparer.Ordinal)
        );
        FlowState state = new (function.Parameters);

        foreach (UserParameterSymbol parameter in function.Parameters)
        {
            functionBinder.scope.TryDeclare(parameter);
        }

        BoundStatement body = functionBinder.BindStatement(function.Declaration.Body, state);

        if (
            function.ReturnType.Kind is not TypeKind.Void and not TypeKind.ErrorRecovery &&
            state.CanCompleteNormally
        )
        {
            Report(
                DiagnosticCodes.NotAllPathsReturn,
                function.Declaration.Body.CloseBraceToken.Span,
                $"Not all paths in function '{function.Name}' return '{function.ReturnType.DisplayName}'.",
                DiagnosticCategory.ControlFlow
            );
        }

        IReadOnlyList<BoundStatement> statements = body is BoundStatement.Block block
            ? [ .. block.Statements ]
            : [ body ];

        return new BoundFunction(function, statements);
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
            ThrowStatementSyntax error => BindThrowStatement(error, state),
            TryStatementSyntax protectedStatement =>
                BindTryStatement(protectedStatement, state),
            EmptyStatementSyntax empty => BindEmptyStatement(empty),
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
        IList<BoundStatement> statements = [ ];

        foreach (StatementSyntax statementSyntax in syntax.Statements)
        {
            BindStatementWithReachability(statementSyntax, state, statements);
        }

        RemoveVariablesFromFlowState(state, blockScope.Variables);

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
        LocalSymbol local = new (
            name,
            localType,
            nextLocalSlot++,
            syntax.IsReadOnly,
            loopDepth
        );
        bool hasCurrentLocal = scope.ContainsVariable(name);
        bool hasVisibleLocal = scope.TryLookup(name, out _);
        bool hasVisibleGlobal = environment.TryGetGlobal(name, out _);
        bool disallowedNestedShadowing =
            hasVisibleLocal &&
            !hasCurrentLocal &&
            !profile.Shadowing.HasFlag(ShadowingPolicy.NestedScopes);
        bool disallowedGlobalShadowing =
            hasVisibleGlobal &&
            !profile.Shadowing.HasFlag(ShadowingPolicy.Globals);

        if (
            hasCurrentLocal ||
            disallowedNestedShadowing ||
            disallowedGlobalShadowing
        )
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
            state.PossiblyAssigned.Add(local);

            if (local.IsReadOnly)
            {
                state.ReadOnlyAssignments.Add((local, syntax.IdentifierToken.Span));
            }
        }

        return new BoundStatement.VariableDeclaration(syntax, local, initializer);
    }

    private TypeSymbol InferLocalType(
        VariableDeclarationStatementSyntax syntax,
        BoundExpression? initializer
    )
    {
        if (initializer is not (null or { Type.Kind: TypeKind.Null or TypeKind.ErrorRecovery or TypeKind.Void }))
        {
            return initializer.Type;
        }

        Report(
            DiagnosticCodes.CannotInferType,
            syntax.Span,
            "The local variable type cannot be inferred from its initializer."
        );
        return TypeSymbols.ErrorRecovery;
    }

    private BoundStatement BindAssignment(
        AssignmentStatementSyntax syntax,
        FlowState state
    )
    {
        BoundExpression target = BindAssignmentTarget(syntax.Target, state);
        BoundExpression value = BindExpression(syntax.Value, target.Type);
        bool isReadOnlyArrayElement =
            target is BoundExpression.ElementAccess
            {
                IsObjectAccess: false,
                Target.Type: ArrayTypeSymbol { IsReadOnly: true } or
                NullableTypeSymbol
                {
                    UnderlyingType: ArrayTypeSymbol { IsReadOnly: true },
                },
            };
        ObjectPropertySymbol? assignedProperty = target switch
        {
            BoundExpression.MemberAccess member => member.Property,
            BoundExpression.ElementAccess { IsObjectAccess: true } element =>
                element.Property,
            _ => null,
        };

        if (
            isReadOnlyArrayElement
        )
        {
            Report(
                DiagnosticCodes.ReadOnlyTarget,
                syntax.Target.Span,
                "An element cannot be assigned through a read-only array view."
            );
        }
        else if (assignedProperty?.IsReadOnly == true)
        {
            Report(
                DiagnosticCodes.ReadOnlyTarget,
                syntax.Target.Span,
                $"Property '{assignedProperty.Name}' is read-only."
            );
        }
        else if (
            target is BoundExpression.MemberAccess { IsArrayLength: false } &&
            !profile.Mutations.HasFlag(MutationFeatures.ObjectProperties)
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledObjectPropertyMutation,
                syntax.EqualToken.Span,
                "Object property assignment is disabled by the language profile."
            );
        }
        else if (
            target is BoundExpression.ElementAccess { IsObjectAccess: true } &&
            !profile.Mutations.HasFlag(MutationFeatures.ObjectProperties)
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledObjectPropertyMutation,
                syntax.EqualToken.Span,
                "Object property assignment is disabled by the language profile."
            );
        }
        else if (
            target is BoundExpression.ElementAccess { IsObjectAccess: false } &&
            !profile.Mutations.HasFlag(MutationFeatures.ArrayElements)
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledArrayElementMutation,
                syntax.EqualToken.Span,
                "Array element assignment is disabled by the language profile."
            );
        }

        if (target is BoundExpression.MemberAccess { IsArrayLength: true })
        {
            Report(
                DiagnosticCodes.ReadOnlyTarget,
                syntax.Target.Span,
                "The intrinsic array length property is read-only."
            );
        }

        if (target is BoundExpression.Local local && value.Type.Kind != TypeKind.ErrorRecovery)
        {
            if (
                state.CanCompleteNormally &&
                local.Symbol.IsReadOnly &&
                state.PossiblyAssigned.Contains(local.Symbol)
            )
            {
                ReportReadOnlyAssignment(
                    local.Symbol,
                    syntax.Target.Span,
                    $"Read-only local variable '{local.Symbol.Name}' may already be assigned on an incoming control-flow path."
                );
            }

            state.Assigned.Add(local.Symbol);
            state.PossiblyAssigned.Add(local.Symbol);

            if (local.Symbol.IsReadOnly)
            {
                state.ReadOnlyAssignments.Add((local.Symbol, syntax.Target.Span));
            }
        }

        return new BoundStatement.Assignment(syntax, target, value);
    }

    private BoundStatement BindRemoval(
        RemovalStatementSyntax syntax,
        FlowState state
    )
    {
        BoundExpression target = BindAssignmentTarget(syntax.Target, state);

        if (!profile.Mutations.HasFlag(MutationFeatures.PropertyRemoval))
        {
            ReportFeature(
                DiagnosticCodes.DisabledPropertyRemoval,
                syntax.TildeToken.Span,
                "Property removal is disabled by the language profile."
            );
        }

        bool isRemovable = target switch
        {
            BoundExpression.MemberAccess member =>
                !member.IsArrayLength &&
                (
                    member.IsDynamic ||
                    member.Property is
                    {
                        IsOptional: true,
                        IsReadOnly: false,
                    }
                ),
            BoundExpression.ElementAccess element =>
                element.IsObjectAccess &&
                (
                    element.IsDynamic ||
                    element.Property is
                    {
                        IsOptional: true,
                        IsReadOnly: false,
                    }
                ),
            _ => false,
        };

        if (!isRemovable && target.Type.Kind != TypeKind.ErrorRecovery)
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

    private BoundStatement BindEmptyStatement(
        EmptyStatementSyntax syntax,
        bool reportWarning = true
    )
    {
        if (
            reportWarning &&
            syntax.SemicolonToken.Kind == TokenKind.Semicolon
        )
        {
            ReportWarning(
                DiagnosticCodes.RedundantEmptyStatement,
                syntax.SemicolonToken.Span,
                "Empty statement is redundant.",
                DiagnosticCategory.ControlFlow
            );
        }

        return new BoundStatement.Empty(syntax);
    }

    private BoundStatement BindIfStatement(IfStatementSyntax syntax, FlowState state)
    {
        BoundExpression condition = BindCondition(syntax.Condition);
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
        if (!profile.Loops.HasFlag(LoopFeatures.While))
        {
            ReportFeature(
                DiagnosticCodes.DisabledWhileLoop,
                syntax.WhileKeyword.Span,
                "While loops are disabled by the language profile."
            );
        }

        FlowState loopEntryState = state.Clone();
        BoundExpression condition = BindCondition(syntax.Condition);
        bool isConstantTrue = IsConstantTrue(condition);
        bool isConstantFalse = IsConstantFalse(condition);
        FlowState bodyState = state.Clone();
        LoopFlowContext loopContext = new ();
        loopContexts.Push(loopContext);
        loopDepth++;
        int currentLoopDepth = loopDepth;
        BoundStatement body = BindEmbeddedStatement(syntax.Body, bodyState);
        loopDepth--;
        loopContexts.Pop();

        List<FlowState> backEdgeStates = [ ];

        if (bodyState.CanCompleteNormally)
        {
            backEdgeStates.Add(bodyState);
        }

        backEdgeStates.AddRange(loopContext.ContinueStates);

        if (!isConstantFalse)
        {
            ReportReadOnlyAssignmentsReachingBackEdge(
                loopEntryState,
                backEdgeStates,
                currentLoopDepth
            );
        }

        List<FlowState> exitStates = [ .. loopContext.BreakStates ];

        if (!isConstantTrue)
        {
            exitStates.Add(loopEntryState);

            if (!isConstantFalse)
            {
                exitStates.AddRange(backEdgeStates);
            }
        }

        ReplaceWithLoopExitStates(state, exitStates);

        return new BoundStatement.While(syntax, condition, body);
    }

    private BoundStatement BindForStatement(
        ForStatementSyntax syntax,
        FlowState state
    )
    {
        if (!profile.Loops.HasFlag(LoopFeatures.For))
        {
            ReportFeature(
                DiagnosticCodes.DisabledForLoop,
                syntax.ForKeyword.Span,
                "For loops are disabled by the language profile."
            );
        }

        BindingScope parentScope = scope;
        BindingScope forScope = new (parentScope);
        scope = forScope;
        BoundStatement? initializer = syntax.Initializer is null
            ? null
            : BindStatement(syntax.Initializer, state);
        FlowState loopEntryState = state.Clone();
        BoundExpression? condition = syntax.Condition is null
            ? null
            : BindCondition(syntax.Condition);
        bool isConstantTrue = condition is null || IsConstantTrue(condition);
        bool isConstantFalse = condition is not null && IsConstantFalse(condition);
        FlowState bodyState = state.Clone();
        LoopFlowContext loopContext = new ();
        loopContexts.Push(loopContext);
        loopDepth++;
        int currentLoopDepth = loopDepth;
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
        loopDepth--;
        loopContexts.Pop();

        if (
            !isConstantFalse &&
            iteratorState.CanCompleteNormally
        )
        {
            ReportReadOnlyAssignmentsReachingBackEdge(
                loopEntryState,
                [ iteratorState ],
                currentLoopDepth
            );
        }

        List<FlowState> exitStates = [ .. loopContext.BreakStates ];

        if (!isConstantTrue)
        {
            exitStates.Add(loopEntryState);

            if (
                !isConstantFalse &&
                iteratorState.CanCompleteNormally
            )
            {
                exitStates.Add(iteratorState);
            }
        }

        ReplaceWithLoopExitStates(state, exitStates);
        RemoveVariablesFromFlowState(state, forScope.Variables);
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
        BoundStatement statement = syntax is EmptyStatementSyntax empty
            ? BindEmptyStatement(empty, false)
            : BindStatement(syntax, state);

        RemoveVariablesFromFlowState(state, embeddedScope.Variables);

        scope = parentScope;
        return statement;
    }

    private BoundStatement BindBreakStatement(
        BreakStatementSyntax syntax,
        FlowState state
    )
    {
        ReportDisabledLoopLevel(
            syntax.LevelSignToken,
            syntax.LevelToken
        );
        int level = BindLoopLevel(syntax.LevelSignToken, syntax.LevelToken);

        if (loopContexts.Count == 0)
        {
            Report(
                DiagnosticCodes.BreakOutsideLoop,
                syntax.Span,
                "A break statement is only valid inside a loop.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (level > loopContexts.Count)
        {
            ReportInvalidLoopLevel(level, loopContexts.Count, syntax.Span);
        }
        else if (
            finallyLoopDepths.TryPeek(out int finallyLoopDepth) &&
            loopDepth - level + 1 <= finallyLoopDepth
        )
        {
            Report(
                DiagnosticCodes.LoopTransferOutsideFinally,
                syntax.Span,
                "A break statement cannot target a loop outside the active finally clause.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (state.CanCompleteNormally)
        {
            GetLoopContext(level).AddBreakState(state);
        }

        state.CanCompleteNormally = false;

        return new BoundStatement.Break(syntax, level);
    }

    private BoundStatement BindContinueStatement(
        ContinueStatementSyntax syntax,
        FlowState state
    )
    {
        ReportDisabledLoopLevel(
            syntax.LevelSignToken,
            syntax.LevelToken
        );
        int level = BindLoopLevel(syntax.LevelSignToken, syntax.LevelToken);

        if (loopContexts.Count == 0)
        {
            Report(
                DiagnosticCodes.ContinueOutsideLoop,
                syntax.Span,
                "A continue statement is only valid inside a loop.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (level > loopContexts.Count)
        {
            ReportInvalidLoopLevel(level, loopContexts.Count, syntax.Span);
        }
        else if (
            finallyLoopDepths.TryPeek(out int finallyLoopDepth) &&
            loopDepth - level + 1 <= finallyLoopDepth
        )
        {
            Report(
                DiagnosticCodes.LoopTransferOutsideFinally,
                syntax.Span,
                "A continue statement cannot target a loop outside the active finally clause.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (state.CanCompleteNormally)
        {
            GetLoopContext(level).AddContinueState(state);
        }

        state.CanCompleteNormally = false;

        return new BoundStatement.Continue(syntax, level);
    }

    private int BindLoopLevel(
        SyntaxToken? signToken,
        SyntaxToken? levelToken
    )
    {
        if (levelToken is null)
        {
            return 1;
        }

        string tokenText = GetText(levelToken);
        string sign = signToken?.Kind switch
        {
            TokenKind.Plus => "+",
            TokenKind.Minus => "-",
            _ => "",
        };
        string text = $"{sign}{tokenText}";

        if (IsPrefixedInteger(tokenText))
        {
            bool parsed = TryParsePrefixedInteger(
                tokenText,
                signToken?.Kind == TokenKind.Minus,
                out long prefixedLevel,
                out bool overflow
            );

            if (parsed && prefixedLevel is > 0 and <= int.MaxValue)
            {
                return (int)prefixedLevel;
            }

            if (!parsed && !overflow)
            {
                return 1;
            }

            ReportInvalidLoopLevel(levelToken);
            return 1;
        }

        if (
            !int.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int level
            ) ||
            level <= 0
        )
        {
            ReportInvalidLoopLevel(levelToken);
            return 1;
        }

        return level;
    }

    private void ReportInvalidLoopLevel(SyntaxToken levelToken)
    {
        Report(
            DiagnosticCodes.InvalidLoopLevel,
            levelToken.Span,
            "The loop level must be a positive integer literal.",
            DiagnosticCategory.ControlFlow
        );
    }

    private LoopFlowContext GetLoopContext(int level)
    {
        return loopContexts.ElementAt(level - 1);
    }

    private void ReportInvalidLoopLevel(
        int level,
        int loopCount,
        TextSpan span
    )
    {
        Report(
            DiagnosticCodes.InvalidLoopLevel,
            span,
            $"Loop level {level} exceeds the {loopCount} enclosing loop(s).",
            DiagnosticCategory.ControlFlow
        );
    }

    private BoundStatement BindReturnStatement(
        ReturnStatementSyntax syntax,
        FlowState state
    )
    {
        if (finallyLoopDepths.Count > 0)
        {
            Report(
                DiagnosticCodes.ReturnInsideFinally,
                syntax.ReturnKeyword.Span,
                "A return statement is not permitted inside a finally clause.",
                DiagnosticCategory.ControlFlow
            );
        }

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
                $"A void {returnContext} cannot return a value.",
                DiagnosticCategory.ControlFlow
            );
        }
        else if (expectedResultType.Kind != TypeKind.Void && expression is null)
        {
            Report(
                DiagnosticCodes.InvalidReturn,
                syntax.Span,
                $"The {returnContext} must return '{expectedResultType.DisplayName}'.",
                DiagnosticCategory.ControlFlow
            );
        }

        state.CanCompleteNormally = false;

        return new BoundStatement.Return(syntax, expression);
    }

    private BoundStatement BindThrowStatement(
        ThrowStatementSyntax syntax,
        FlowState state
    )
    {
        ReportExceptionHandlingFeature(syntax.ThrowKeyword);
        BoundExpression error;

        if (syntax.Expression is null)
        {
            if (currentCatchError is null)
            {
                Report(
                    DiagnosticCodes.RethrowOutsideCatch,
                    syntax.ThrowKeyword.Span,
                    "A rethrow statement is only valid inside a catch clause.",
                    DiagnosticCategory.ControlFlow
                );
                error = new BoundExpression.Error(syntax);
            }
            else
            {
                error = new BoundExpression.Local(syntax, currentCatchError);
            }
        }
        else
        {
            error = syntax.Expression is ObjectLiteralExpressionSyntax objectLiteral
                ? BindObjectExpression(
                    objectLiteral,
                    null,
                    isErrorPrototypeContext: true
                )
                : BindExpression(syntax.Expression);

            if (
                error.Type.Kind is not TypeKind.ErrorRecovery and not TypeKind.ErrorValue &&
                !IsErrorPrototype(
                    error.Type, new HashSet<TypeSymbol>(
                        ReferenceEqualityComparer.Instance
                    )
                )
            )
            {
                Report(
                    DiagnosticCodes.InvalidThrowOperand,
                    syntax.Expression.Span,
                    "A throw operand must be an error value or a compatible error prototype."
                );
            }

            if (syntax.Expression is ObjectLiteralExpressionSyntax inlineObject)
            {
                ReportUnrecognizedErrorProperties(inlineObject);
            }
        }

        state.CanCompleteNormally = false;
        return new BoundStatement.Throw(syntax, error);
    }

    private BoundStatement BindTryStatement(
        TryStatementSyntax syntax,
        FlowState state
    )
    {
        ReportExceptionHandlingFeature(syntax.TryKeyword);

        if (syntax.CatchClause is { } catchClause)
        {
            ReportExceptionHandlingFeature(catchClause.CatchKeyword);
        }

        if (syntax.FinallyClause is { } finallyClause)
        {
            ReportExceptionHandlingFeature(finallyClause.FinallyKeyword);
        }

        FlowState entryState = state.Clone();
        FlowState protectedState = entryState.Clone();
        BoundStatement protectedStatement = BindStatement(
            syntax.Body,
            protectedState
        );
        BoundStatement? handler = null;
        LocalSymbol? errorLocal = null;
        FlowState? handlerState = null;

        if (syntax.CatchClause is { } handlerSyntax)
        {
            BindingScope parentScope = scope;
            BindingScope handlerScope = new (parentScope);
            scope = handlerScope;
            handlerState = entryState.Clone();
            string errorName = handlerSyntax.IdentifierToken is null
                ? $"$error{nextLocalSlot.ToString(CultureInfo.InvariantCulture)}"
                : GetText(handlerSyntax.IdentifierToken);
            errorLocal = new LocalSymbol(
                errorName,
                TypeSymbols.ErrorValue,
                nextLocalSlot++,
                true,
                loopDepth
            );
            handlerState.Assigned.Add(errorLocal);
            handlerState.PossiblyAssigned.Add(errorLocal);

            if (handlerSyntax.IdentifierToken is not null)
            {
                bool shadowsLocal = parentScope.TryLookup(errorName, out _);
                bool shadowsGlobal = environment.TryGetGlobal(errorName, out _);

                if (
                    shadowsLocal &&
                    !profile.Shadowing.HasFlag(ShadowingPolicy.NestedScopes) ||
                    shadowsGlobal &&
                    !profile.Shadowing.HasFlag(ShadowingPolicy.Globals)
                )
                {
                    Report(
                        DiagnosticCodes.ShadowedVariable,
                        handlerSyntax.IdentifierToken.Span,
                        $"Catch parameter '{errorName}' cannot shadow another visible variable."
                    );
                }

                if (!handlerScope.TryDeclare(errorLocal))
                {
                    throw new InvalidOperationException(
                        "A new catch scope unexpectedly contains the catch parameter."
                    );
                }
            }

            LocalSymbol? previousCatchError = currentCatchError;
            currentCatchError = errorLocal;
            handler = BindStatement(handlerSyntax.Body, handlerState);
            currentCatchError = previousCatchError;
            RemoveVariablesFromFlowState(handlerState, handlerScope.Variables);
            handlerState.Assigned.Remove(errorLocal);
            handlerState.PossiblyAssigned.Remove(errorLocal);
            scope = parentScope;
        }

        List<FlowState> normalStates = [ ];

        if (protectedState.CanCompleteNormally)
        {
            normalStates.Add(protectedState);
        }

        if (handlerState?.CanCompleteNormally == true)
        {
            normalStates.Add(handlerState);
        }

        BoundStatement? cleanup = null;

        if (syntax.FinallyClause is { } cleanupSyntax)
        {
            List<FlowState> cleanupEntryStates = [ entryState, .. normalStates ];
            FlowState cleanupState = CreateIntersectionState(cleanupEntryStates);
            FlowState cleanupEntryState = cleanupState.Clone();
            finallyLoopDepths.Push(loopDepth);
            cleanup = BindStatement(cleanupSyntax.Body, cleanupState);
            finallyLoopDepths.Pop();

            if (cleanupState.CanCompleteNormally && normalStates.Count > 0)
            {
                FlowState normalState = CreateIntersectionState(normalStates);
                ApplyFlowEffects(cleanupEntryState, cleanupState, normalState);
                ReplaceWithIntersection(state, [ normalState ]);
            }
            else
            {
                state.CanCompleteNormally = false;
            }
        }
        else if (normalStates.Count > 0)
        {
            ReplaceWithIntersection(
                state,
                [ CreateIntersectionState(normalStates) ]
            );
        }
        else
        {
            state.CanCompleteNormally = false;
        }

        return new BoundStatement.Try(
            syntax,
            protectedStatement,
            errorLocal,
            handler,
            cleanup
        );
    }

    private static void ApplyFlowEffects(
        FlowState before,
        FlowState after,
        FlowState target
    )
    {
        target.Assigned.UnionWith(after.Assigned.Except(before.Assigned));
        target.PossiblyAssigned.UnionWith(
            after.PossiblyAssigned.Except(before.PossiblyAssigned)
        );
        target.ReadOnlyAssignments.UnionWith(after.ReadOnlyAssignments);
    }

    private static bool IsErrorPrototype(
        TypeSymbol type,
        ISet<TypeSymbol> active
    )
    {
        TypeSymbol nonNullable = GetNonNullable(type);

        if (nonNullable.Kind == TypeKind.ErrorValue)
        {
            return true;
        }

        if (nonNullable is not ObjectTypeSymbol objectType)
        {
            return false;
        }

        if (!active.Add(objectType))
        {
            return true;
        }

        bool valid =
            objectType.TryGetProperty("code", out ObjectPropertySymbol? code) &&
            !code.IsOptional &&
            TypeRelations.IsAssignable(code.Type, TypeSymbols.String) &&
            objectType.TryGetProperty(
                "message",
                out ObjectPropertySymbol? message
            ) &&
            !message.IsOptional &&
            TypeRelations.IsAssignable(message.Type, TypeSymbols.String);

        if (
            valid &&
            objectType.TryGetProperty("cause", out ObjectPropertySymbol? cause)
        )
        {
            valid =
                cause.Type.Kind == TypeKind.Null ||
                IsErrorPrototype(cause.Type, active);
        }

        if (
            valid &&
            objectType.TryGetProperty("data", out ObjectPropertySymbol? data)
        )
        {
            valid = data.Type.Kind != TypeKind.Void;
        }

        active.Remove(objectType);
        return valid;
    }

    private void ReportUnrecognizedErrorProperties(
        ObjectLiteralExpressionSyntax syntax
    )
    {
        foreach (ObjectPropertyInitializerSyntax property in syntax.Properties)
        {
            string name = property.NameToken.Kind == TokenKind.StringLiteral
                ? property.NameToken.Value ?? ""
                : GetText(property.NameToken);

            if (name is "code" or "message" or "cause" or "data")
            {
                if (
                    name == "cause" &&
                    property.Value is ObjectLiteralExpressionSyntax cause
                )
                {
                    ReportUnrecognizedErrorProperties(cause);
                }

                continue;
            }

            ReportWarning(
                DiagnosticCodes.UnrecognizedErrorProperty,
                property.NameToken.Span,
                $"Property '{name}' is ignored when the error is thrown."
            );
        }
    }

    private void ReportExceptionHandlingFeature(SyntaxToken token)
    {
        if (
            profile is
            {
                LanguageVersion: >= LanguageVersion.Version1_2,
                ExceptionHandling: ExceptionHandlingFeature.Enabled,
            }
        )
        {
            return;
        }

        ReportFeature(
            profile.LanguageVersion < LanguageVersion.Version1_2
                ? DiagnosticCodes.UnsupportedLanguageVersionFeature
                : DiagnosticCodes.DisabledExceptionHandling,
            token.Span,
            profile.LanguageVersion < LanguageVersion.Version1_2
                ? "Exception-handling syntax requires language version 1.2."
                : "Exception handling is disabled by the language profile."
        );
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
            BinaryExpressionSyntax
                {
                    OperatorToken.Kind: TokenKind.QuestionQuestion,
                }
                binary => BindCoalescingExpression(binary, expectedType),
            BinaryExpressionSyntax binary => BindBinaryExpression(binary),
            ConversionExpressionSyntax conversion =>
                BindConversionExpression(conversion, expectedType),
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

        if (expectedType is null || expression.Type.Kind == TypeKind.ErrorRecovery)
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
                string tokenText = GetText(syntax.LiteralToken);
                bool isNegative = syntax.SignToken?.Kind == TokenKind.Minus;

                if (IsPrefixedInteger(tokenText))
                {
                    if (
                        TryParsePrefixedInteger(
                            tokenText,
                            isNegative,
                            out long prefixedValue,
                            out bool overflow
                        )
                    )
                    {
                        return new BoundExpression.Literal(
                            syntax,
                            TypeSymbols.Int,
                            prefixedValue
                        );
                    }

                    if (overflow)
                    {
                        string overflowText = GetSignedLiteralText(syntax);
                        Report(
                            DiagnosticCodes.InvalidIntegerLiteral,
                            syntax.Span,
                            $"Integer literal '{overflowText}' is outside the supported range."
                        );
                    }

                    return new BoundExpression.Error(syntax);
                }

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
                        DiagnosticCodes.InvalidFloatLiteral,
                        syntax.Span,
                        $"Float literal '{text}' is outside the supported range."
                    );

                    return new BoundExpression.Error(syntax);
                }

                return new BoundExpression.Literal(syntax, TypeSymbols.Float, value);
            }

            case TokenKind.InftyKeyword:
            {
                double value = syntax.SignToken?.Kind == TokenKind.Minus
                    ? double.NegativeInfinity
                    : double.PositiveInfinity;

                return new BoundExpression.Literal(syntax, TypeSymbols.Float, value);
            }

            case TokenKind.NanKeyword:
            {
                return new BoundExpression.Literal(
                    syntax,
                    TypeSymbols.Float,
                    double.NaN
                );
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

        if (scope.TryLookup(name, out BoundVariableSymbol? variable))
        {
            if (
                currentFlowState.CanCompleteNormally &&
                !currentFlowState.Assigned.Contains(variable)
            )
            {
                Report(
                    DiagnosticCodes.UnassignedLocal,
                    syntax.Span,
                    $"Local variable '{name}' is used before it is definitely assigned.",
                    DiagnosticCategory.ControlFlow
                );
            }

            return variable switch
            {
                LocalSymbol local => new BoundExpression.Local(syntax, local),
                UserParameterSymbol parameter =>
                    new BoundExpression.Parameter(syntax, parameter),
                _ => throw new InvalidOperationException("Unknown bound variable symbol."),
            };
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
        ReportDisabledTrailingComma(
            syntax.CommaTokens,
            syntax.CommaTokens.Count == syntax.Elements.Count
        );
        ArrayTypeSymbol? expectedArray = GetNonNullable(expectedType) as ArrayTypeSymbol;
        ArrayTypeSymbol? expectedElementArray =
            !syntax.IsReadOnly || expectedArray?.IsReadOnly == true
                ? expectedArray
                : null;
        IList<BoundExpression> elements = [ ];
        TypeSymbol? elementType = null;
        bool hasInvalidElementType = false;

        foreach (ExpressionSyntax elementSyntax in syntax.Elements)
        {
            BoundExpression element = BindExpression(
                elementSyntax,
                expectedElementArray?.ElementType
            );
            elements.Add(element);

            if (element.Type.Kind == TypeKind.ErrorRecovery)
            {
                hasInvalidElementType = true;
                continue;
            }

            if (expectedElementArray is not null)
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
                : GetCommonType(elementType, element.Type);

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

        if (expectedElementArray is not null)
        {
            ArrayTypeSymbol literalType = syntax.IsReadOnly
                ? TypeSymbols.ReadOnlyArray(expectedElementArray.ElementType)
                : TypeSymbols.Array(expectedElementArray.ElementType);

            return new BoundExpression.Array(
                syntax,
                literalType,
                elements.AsReadOnly()
            );
        }

        if (
            (elementType is null && !hasInvalidElementType) ||
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
                ? CreateArrayType(TypeSymbols.Unknown, syntax.IsReadOnly)
                : CreateArrayType(elementType, syntax.IsReadOnly);

        if (!hasInvalidElementType)
        {
            for (int index = 0; index < elements.Count; index++)
            {
                elements[index] = ConvertImplicit(elements[index], arrayType.ElementType);
            }
        }

        return new BoundExpression.Array(syntax, arrayType, elements.AsReadOnly());

        static ArrayTypeSymbol CreateArrayType(TypeSymbol elementType, bool isReadOnly)
        {
            return isReadOnly
                ? TypeSymbols.ReadOnlyArray(elementType)
                : TypeSymbols.Array(elementType);
        }
    }

    private BoundExpression BindObjectExpression(
        ObjectLiteralExpressionSyntax syntax,
        TypeSymbol? expectedType,
        bool isErrorPrototypeContext = false
    )
    {
        ReportDisabledTrailingComma(
            syntax.CommaTokens,
            syntax.CommaTokens.Count == syntax.Properties.Count
        );

        if (
            syntax.IsOpen &&
            profile.OpenObjects != OpenObjectsFeature.Enabled
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledOpenObjects,
                syntax.OpenBraceToken.Span,
                "Open object literals are disabled by the language profile."
            );
        }

        ObjectTypeSymbol? expectedObject = GetNonNullable(expectedType) as ObjectTypeSymbol;
        bool isFullSyntax = profile is
        {
            LanguageVersion: >= LanguageVersion.Version1_2,
            ObjectLiteralSyntax: ObjectLiteralSyntax.Full,
        };
        IList<BoundExpression.ObjectProperty> properties = [ ];
        ICollection<ObjectPropertySymbol> propertySymbols = [ ];
        ISet<string> names = new HashSet<string>(StringComparer.Ordinal);

        if (
            !isFullSyntax &&
            expectedObject is not null &&
            syntax.IsOpen != expectedObject.IsOpen
        )
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

            TypeSymbol? declaredType = propertySyntax.Type is null
                ? null
                : BindType(propertySyntax.Type);
            TypeSymbol? errorPrototypePropertyType =
                isErrorPrototypeContext
                    ? GetErrorPrototypePropertyType(name, propertySyntax.Value)
                    : null;
            bool includeProperty =
                !isErrorPrototypeContext ||
                IsRecognizedErrorPrototypeProperty(name);
            TypeSymbol? expectedPropertyType = declaredType ??
                errorPrototypePropertyType ??
                expectedProperty?.Type;

            if (
                expectedPropertyType is null &&
                expectedObject?.IsOpen == true &&
                !isFullSyntax
            )
            {
                expectedPropertyType = TypeSymbols.Nullable(TypeSymbols.Unknown);
            }

            if (
                !isFullSyntax &&
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

            BoundExpression? value = propertySyntax.Value switch
            {
                null => null,
                ObjectLiteralExpressionSyntax nestedPrototype
                    when isErrorPrototypeContext && name == "cause" =>
                    BindObjectExpression(
                        nestedPrototype,
                        null,
                        isErrorPrototypeContext: true
                    ),
                _ => BindExpression(
                    propertySyntax.Value,
                    expectedPropertyType
                ),
            };
            bool isFirstDeclaration = names.Add(name);

            if (!isFirstDeclaration)
            {
                Report(
                    DiagnosticCodes.DuplicateObjectProperty,
                    propertySyntax.NameToken.Span,
                    $"Object property '{name}' is declared more than once."
                );
            }

            bool isOptional = propertySyntax.IsLegacy && expectedProperty is not null
                ? expectedProperty.IsOptional
                : propertySyntax.IsOptional;
            bool isReadOnly = propertySyntax.IsLegacy && expectedProperty is not null
                ? expectedProperty.IsReadOnly
                : propertySyntax.IsReadOnly;

            if (value is null && !isOptional)
            {
                Report(
                    DiagnosticCodes.MissingObjectPropertyInitializer,
                    propertySyntax.Span,
                    $"Required object property '{name}' requires an initializer."
                );
            }

            if (value is null && declaredType is null)
            {
                Report(
                    DiagnosticCodes.MissingObjectPropertyType,
                    propertySyntax.Span,
                    $"Uninitialized optional object property '{name}' requires an explicit type."
                );
            }

            TypeSymbol propertyType = declaredType?.Kind == TypeKind.ErrorRecovery
                ? TypeSymbols.Unknown
                : declaredType ??
                (
                    value?.Type.Kind is
                        TypeKind.Null or
                        TypeKind.Void or
                        TypeKind.ErrorRecovery or
                        null
                        ? TypeSymbols.Unknown
                        : value.Type
                );

            if (value?.Type.Kind == TypeKind.Null && declaredType is null)
            {
                Report(
                    DiagnosticCodes.CannotInferType,
                    propertySyntax.Value!.Span,
                    $"The type of object property '{name}' cannot be inferred from null."
                );
            }
            else if (value?.Type.Kind == TypeKind.Void)
            {
                Report(
                    DiagnosticCodes.InvalidVoidExpression,
                    propertySyntax.Value!.Span,
                    "A void expression cannot initialize an object property."
                );
            }

            if (value is not null && includeProperty)
            {
                properties.Add(new BoundExpression.ObjectProperty(name, value));
            }

            if (isFirstDeclaration && includeProperty)
            {
                propertySymbols.Add(
                    new ObjectPropertySymbol(
                        name,
                        propertyType,
                        isOptional,
                        isReadOnly
                    )
                );
            }
        }

        if (!isFullSyntax && expectedObject is not null)
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

        ObjectTypeSymbol objectType = !isFullSyntax && expectedObject is not null
            ? expectedObject
            : ObjectTypeSymbol.CreateAnonymous(syntax.IsOpen, propertySymbols);

        return new BoundExpression.Object(syntax, objectType, properties.AsReadOnly());
    }

    private static TypeSymbol? GetErrorPrototypePropertyType(
        string name,
        ExpressionSyntax? value
    )
    {
        return name switch
        {
            "code" or "message" => TypeSymbols.String,
            "cause" when value is LiteralExpressionSyntax
            {
                LiteralToken.Kind: TokenKind.NullKeyword,
            } => TypeSymbols.Nullable(TypeSymbols.ErrorValue),
            "data" => TypeSymbols.Nullable(TypeSymbols.Unknown),
            _ => null,
        };
    }

    private static bool IsRecognizedErrorPrototypeProperty(string name)
    {
        return name is "code" or "message" or "cause" or "data";
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax syntax)
    {
        BoundExpression operand = BindExpression(syntax.Operand);

        if (
            syntax.OperatorToken.Kind == TokenKind.Bang &&
            profile.ConditionSemantics == ConditionSemantics.Truthiness
        )
        {
            if (operand.Type.Kind == TypeKind.ErrorRecovery)
            {
                return new BoundExpression.Error(syntax);
            }

            if (operand.Type.Kind == TypeKind.Void)
            {
                ReportOperatorNotDefined(
                    syntax.OperatorToken,
                    operand.Type
                );
                return new BoundExpression.Error(syntax);
            }

            operand = NormalizeTruthiness(operand);

            BoundExpression.Unary truthinessResult = new (
                syntax,
                TypeSymbols.Bool,
                syntax.OperatorToken.Kind,
                operand
            );
            ReportConstantCondition(truthinessResult, syntax.Span);
            return truthinessResult;
        }

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

        if (operand.Type.Kind == TypeKind.ErrorRecovery)
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

        BoundExpression.Unary result = new (
            syntax,
            resultType,
            syntax.OperatorToken.Kind,
            operand
        );

        if (syntax.OperatorToken.Kind == TokenKind.Bang)
        {
            ReportConstantCondition(result, syntax.Span);
        }

        return result;
    }

    private BoundExpression BindBinaryExpression(BinaryExpressionSyntax syntax)
    {
        BoundExpression left = BindExpression(syntax.Left);
        BoundExpression right = BindExpression(syntax.Right);

        if (left.Type.Kind == TypeKind.ErrorRecovery || right.Type.Kind == TypeKind.ErrorRecovery)
        {
            return new BoundExpression.Error(syntax);
        }

        if (
            syntax.OperatorToken.Kind is
                TokenKind.AmpersandAmpersand or
                TokenKind.PipePipe &&
            profile.ConditionSemantics == ConditionSemantics.Truthiness
        )
        {
            if (
                left.Type.Kind == TypeKind.Void ||
                right.Type.Kind == TypeKind.Void
            )
            {
                ReportOperatorNotDefined(
                    syntax.OperatorToken,
                    left.Type,
                    right.Type
                );
                return new BoundExpression.Error(syntax);
            }

            BoundExpression.Binary truthinessResult = new (
                syntax,
                TypeSymbols.Bool,
                NormalizeTruthiness(left),
                syntax.OperatorToken.Kind,
                NormalizeTruthiness(right)
            );
            ReportLogicalExpression(truthinessResult, syntax.OperatorToken);
            return truthinessResult;
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

        BoundExpression.Binary result = new (
            syntax,
            resultType,
            left,
            syntax.OperatorToken.Kind,
            right
        );
        ReportLogicalExpression(result, syntax.OperatorToken);
        ReportConstantNullComparison(result, syntax.OperatorToken);
        return result;
    }

    private BoundExpression BindCoalescingExpression(
        BinaryExpressionSyntax syntax,
        TypeSymbol? expectedType
    )
    {
        BoundExpression left = BindExpression(syntax.Left);
        BoundExpression right = BindExpression(syntax.Right, expectedType);

        if (left.Type.Kind == TypeKind.ErrorRecovery || right.Type.Kind == TypeKind.ErrorRecovery)
        {
            return new BoundExpression.Error(syntax);
        }

        TypeSymbol? leftValueType = left switch
        {
            { Type: NullableTypeSymbol nullable } => nullable.UnderlyingType,
            BoundExpression.Literal { Type.Kind: TypeKind.Null } => null,
            _ => TypeSymbols.ErrorRecovery,
        };

        if (ReferenceEquals(leftValueType, TypeSymbols.ErrorRecovery))
        {
            ReportOperatorNotDefined(
                syntax.OperatorToken,
                left.Type,
                right.Type
            );
            return new BoundExpression.Error(syntax);
        }

        if (right.Type.Kind == TypeKind.Void)
        {
            ReportOperatorNotDefined(
                syntax.OperatorToken,
                left.Type,
                right.Type
            );
            return new BoundExpression.Error(syntax);
        }

        if (expectedType is not null)
        {
            bool leftIsAssignable = true;
            bool rightIsAssignable = TypeRelations.IsAssignable(
                right.Type,
                expectedType
            );

            if (
                leftValueType is not null &&
                !TypeRelations.IsAssignable(leftValueType, expectedType)
            )
            {
                ReportTypeMismatch(
                    syntax.Left.Span,
                    leftValueType,
                    expectedType
                );
                leftIsAssignable = false;
            }

            if (!leftIsAssignable || !rightIsAssignable)
            {
                return new BoundExpression.Error(syntax);
            }
        }

        TypeSymbol? resultType = expectedType ??
        (
            leftValueType is null
                ? right.Type
                : GetCommonType(leftValueType, right.Type)
        );

        if (resultType is null)
        {
            Report(
                DiagnosticCodes.TypeMismatch,
                syntax.Span,
                $"Null-coalescing operands '{left.Type.DisplayName}' and '{right.Type.DisplayName}' have no common result type."
            );
            return new BoundExpression.Error(syntax);
        }

        right = ConvertImplicit(right, resultType);
        ReportNullCoalescing(left, syntax.Span);

        return new BoundExpression.Coalescing(
            syntax,
            resultType,
            left,
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
            return GetNumericResultType(left, right);
        }

        if (
            operatorKind is
                TokenKind.LeftShift or
                TokenKind.RightShift &&
            left.Kind == TypeKind.Int &&
            right.Kind == TypeKind.Int
        )
        {
            return TypeSymbols.Int;
        }

        if (
            operatorKind is
                TokenKind.Ampersand or
                TokenKind.Caret or
                TokenKind.Pipe &&
            left.Kind == right.Kind &&
            left.Kind is TypeKind.Int or TypeKind.Bool
        )
        {
            return left;
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
        ConversionExpressionSyntax syntax,
        TypeSymbol? expectedType
    )
    {
        BoundExpression expression = BindExpression(syntax.Expression);
        TypeSymbol targetType = BindType(syntax.Type);

        if (!TypeRelations.IsCastable(expression.Type, targetType))
        {
            Report(
                DiagnosticCodes.InvalidConversion,
                syntax.Span,
                $"Type '{expression.Type.DisplayName}' cannot be cast to '{targetType.DisplayName}'."
            );
            return new BoundExpression.Error(syntax);
        }

        if (TypeRelations.AreEquivalent(expression.Type, targetType))
        {
            ReportWarning(
                DiagnosticCodes.RedundantCast,
                syntax.Span,
                $"Cast to equivalent type '{targetType.DisplayName}' is redundant."
            );
            return expression;
        }

        bool isStaticallyGuaranteed = TypeRelations.IsAssignable(
            expression.Type,
            targetType
        );

        if (
            isStaticallyGuaranteed &&
            expectedType is not null &&
            TypeRelations.IsAssignable(expression.Type, expectedType) &&
            TypeRelations.IsAssignable(targetType, expectedType)
        )
        {
            ReportWarning(
                DiagnosticCodes.RedundantCast,
                syntax.Span,
                $"Cast to '{targetType.DisplayName}' is redundant in the expected '{expectedType.DisplayName}' context."
            );
        }

        ConversionKind conversion = isStaticallyGuaranteed
            ? ConversionKind.Implicit
            : ConversionKind.Checked;

        return new BoundExpression.Conversion(
            syntax,
            targetType,
            expression,
            conversion,
            true
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
        else if (!TypeRelations.IsCastable(expression.Type, testedType))
        {
            ReportWarning(
                DiagnosticCodes.ImpossibleTypeTest,
                syntax.Span,
                $"Type test from '{expression.Type.DisplayName}' to '{testedType.DisplayName}' is statically known to be false."
            );
        }
        else if (
            BoundExpressionFacts.TryGetTypeTestResult(
                expression,
                testedType,
                out bool value
            ) &&
            value
        )
        {
            ReportWarning(
                DiagnosticCodes.AlwaysTrueTypeTest,
                syntax.Span,
                $"Type test from '{expression.Type.DisplayName}' to '{testedType.DisplayName}' is statically known to be true."
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
        bool isKnownPropertyTest =
            targetType is ObjectTypeSymbol objectType &&
            key is BoundExpression.Literal
            {
                Type.Kind: TypeKind.String,
                Value: string propertyName,
            } &&
            objectType.TryGetProperty(propertyName, out _);

        if (
            profile.OpenObjects == OpenObjectsFeature.Disabled &&
            targetType.Kind is TypeKind.Object or TypeKind.StructuredObject &&
            !isKnownPropertyTest
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledOpenObjects,
                syntax.HasKeyword.Span,
                "Dynamic object property tests are disabled by the language profile."
            );
        }

        if (
            target.Type.Kind != TypeKind.ErrorRecovery &&
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
        BoundExpression condition = BindCondition(syntax.Condition);
        BoundExpression whenTrue = BindExpression(syntax.WhenTrue, expectedType);
        BoundExpression whenFalse = BindExpression(syntax.WhenFalse, expectedType);
        TypeSymbol? resultType = expectedType ??
            GetCommonType(whenTrue.Type, whenFalse.Type);

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

    private BoundExpression BindCondition(ExpressionSyntax syntax)
    {
        if (profile.ConditionSemantics == ConditionSemantics.StrictBoolean)
        {
            BoundExpression strictCondition = BindExpression(
                syntax,
                TypeSymbols.Bool
            );
            ReportConstantCondition(strictCondition, syntax.Span);
            return strictCondition;
        }

        BoundExpression expression = BindExpression(syntax);

        if (expression.Type.Kind == TypeKind.ErrorRecovery)
        {
            return expression;
        }

        if (expression.Type.Kind == TypeKind.Void)
        {
            ReportTypeMismatch(syntax.Span, expression.Type, TypeSymbols.Bool);
            return expression;
        }

        BoundExpression condition = NormalizeTruthiness(expression);
        ReportConstantCondition(condition, syntax.Span);
        return condition;
    }

    private static BoundExpression NormalizeTruthiness(BoundExpression expression)
    {
        return expression.Type.Kind == TypeKind.Bool
            ? expression
            : new BoundExpression.Truthiness(expression.Syntax, expression);
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax syntax)
    {
        FunctionSymbol? providerFunction = null;
        UserFunctionSymbol? userFunction = null;

        if (syntax.Target is NameExpressionSyntax nameSyntax)
        {
            string name = GetText(nameSyntax.IdentifierToken);

            if (!userFunctions.TryGetValue(name, out userFunction) &&
                !environment.TryGetFunction(name, out providerFunction))
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
                "Only a function name can be called."
            );
        }

        IList<BoundExpression> arguments = [ ];

        for (int index = 0; index < syntax.Arguments.Count; index++)
        {
            TypeSymbol? parameterType =
                userFunction is not null && index < userFunction.Parameters.Count
                    ? userFunction.Parameters[index].Type
                    : providerFunction is not null &&
                    index < providerFunction.Parameters.Count
                        ? providerFunction.Parameters[index].Type
                        : null;
            arguments.Add(BindExpression(syntax.Arguments[index], parameterType));
        }

        if (userFunction is null && providerFunction is null)
        {
            return new BoundExpression.Error(syntax);
        }

        int parameterCount = userFunction?.Parameters.Count ??
            providerFunction?.Parameters.Count ??
            0;
        string functionName = userFunction?.Name ??
            providerFunction?.Name ??
            "";

        if (syntax.Arguments.Count != parameterCount)
        {
            Report(
                DiagnosticCodes.ArgumentCountMismatch,
                syntax.Span,
                $"Function '{functionName}' expects {parameterCount} arguments but received {syntax.Arguments.Count}."
            );
        }

        if (
            userFunction is not null &&
            profile.UserDefinedFunctions == UserDefinedFunctionsFeature.Disabled
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledUserDefinedFunctions,
                syntax.Target.Span,
                "User-defined function calls are disabled by the language profile."
            );
        }
        else if (
            providerFunction is not null &&
            profile.ProviderFunctionCalls == ProviderFunctionCallsFeature.Disabled
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledProviderFunctionCalls,
                syntax.Target.Span,
                "Provider function calls are disabled by the language profile."
            );
        }

        if (
            currentFunctionId is not null &&
            userFunction is not null &&
            userFunctionCalls.TryGetValue(
                currentFunctionId,
                out ISet<string>? calledFunctions
            )
        )
        {
            calledFunctions.Add(userFunction.Id);
        }

        return userFunction is not null
            ? new BoundExpression.UserCall(
                syntax,
                userFunction,
                arguments.AsReadOnly()
            )
            : new BoundExpression.ProviderCall(
                syntax,
                providerFunction ??
                throw new InvalidOperationException("Expected a provider function."),
                arguments.AsReadOnly()
            );
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax syntax)
    {
        BoundExpression target = BindExpression(syntax.Target);
        string name = GetText(syntax.NameToken);
        TypeSymbol targetType = GetNonNullable(target.Type);

        if (targetType is ArrayTypeSymbol && name == "length")
        {
            ReportOptionalAccess(
                target,
                syntax.IsOptional,
                false,
                syntax.OperatorToken.Span
            );
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

        if (
            targetType.Kind == TypeKind.ErrorValue &&
            TryGetErrorProperty(name, out ObjectPropertySymbol? errorProperty)
        )
        {
            ReportOptionalAccess(
                target,
                syntax.IsOptional,
                errorProperty!.IsOptional,
                syntax.OperatorToken.Span
            );
            TypeSymbol resultType = ShouldOptionalAccessReturnNullable(
                syntax.IsOptional,
                target.Type,
                errorProperty.IsOptional
            )
                ? MakeNullable(errorProperty.Type)
                : errorProperty.Type;

            return new BoundExpression.MemberAccess(
                syntax,
                resultType,
                target,
                name,
                errorProperty,
                false,
                syntax.IsOptional,
                false
            );
        }

        if (targetType is ObjectTypeSymbol objectType)
        {
            if (objectType.TryGetProperty(name, out ObjectPropertySymbol? property))
            {
                ReportOptionalAccess(
                    target,
                    syntax.IsOptional,
                    property.IsOptional,
                    syntax.OperatorToken.Span
                );
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
                ReportOptionalAccess(
                    target,
                    syntax.IsOptional,
                    true,
                    syntax.OperatorToken.Span
                );
                if (profile.OpenObjects != OpenObjectsFeature.Enabled)
                {
                    ReportFeature(
                        DiagnosticCodes.DisabledOpenObjects,
                        syntax.NameToken.Span,
                        "Dynamic object member access is disabled by the language profile."
                    );
                }

                return new BoundExpression.MemberAccess(
                    syntax,
                    TypeSymbols.Nullable(TypeSymbols.Unknown),
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
            ReportOptionalAccess(
                target,
                syntax.IsOptional,
                true,
                syntax.OperatorToken.Span
            );
            if (profile.OpenObjects != OpenObjectsFeature.Enabled)
            {
                ReportFeature(
                    DiagnosticCodes.DisabledOpenObjects,
                    syntax.NameToken.Span,
                    "Dynamic object member access is disabled by the language profile."
                );
            }

            return new BoundExpression.MemberAccess(
                syntax,
                TypeSymbols.Nullable(TypeSymbols.Unknown),
                target,
                name,
                null,
                true,
                syntax.IsOptional,
                false
            );
        }

        if (target.Type.Kind != TypeKind.ErrorRecovery)
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
            ReportOptionalAccess(
                target,
                syntax.IsOptional,
                false,
                syntax.OpenBracketToken.Span
            );
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

        if (targetType.Kind == TypeKind.ErrorValue)
        {
            BoundExpression index = BindExpression(syntax.Index, TypeSymbols.String);
            ObjectPropertySymbol? property =
                index is BoundExpression.Literal
                {
                    Type.Kind: TypeKind.String,
                    Value: string propertyName,
                } &&
                TryGetErrorProperty(propertyName, out ObjectPropertySymbol? resolved)
                    ? resolved
                    : null;

            if (property is null)
            {
                Report(
                    DiagnosticCodes.PropertyNotFound,
                    syntax.Index.Span,
                    $"The selected property is not available on '{target.Type.DisplayName}'."
                );
                return new BoundExpression.Error(syntax);
            }

            ReportOptionalAccess(
                target,
                syntax.IsOptional,
                property.IsOptional,
                syntax.OpenBracketToken.Span
            );
            TypeSymbol resultType = ShouldOptionalAccessReturnNullable(
                syntax.IsOptional,
                target.Type,
                property.IsOptional
            )
                ? MakeNullable(property.Type)
                : property.Type;

            return new BoundExpression.ElementAccess(
                syntax,
                resultType,
                target,
                index,
                property,
                true,
                false,
                syntax.IsOptional
            );
        }

        if (targetType is ObjectTypeSymbol objectType)
        {
            BoundExpression index = BindExpression(syntax.Index, TypeSymbols.String);
            ObjectPropertySymbol? property = TryResolveLiteralProperty(index, objectType);
            bool isDynamic = property is null && objectType.IsOpen;
            ReportOptionalAccess(
                target,
                syntax.IsOptional,
                property?.IsOptional == true || isDynamic,
                syntax.OpenBracketToken.Span
            );

            if (
                isDynamic &&
                profile.OpenObjects != OpenObjectsFeature.Enabled
            )
            {
                ReportFeature(
                    DiagnosticCodes.DisabledOpenObjects,
                    syntax.OpenBracketToken.Span,
                    "Dynamic object element access is disabled by the language profile."
                );
            }

            if (property is null && !isDynamic)
            {
                Report(
                    DiagnosticCodes.PropertyNotFound,
                    syntax.Index.Span,
                    $"The selected property is not available on '{target.Type.DisplayName}'."
                );
                return new BoundExpression.Error(syntax);
            }

            TypeSymbol propertyType = property?.Type ??
                TypeSymbols.Nullable(TypeSymbols.Unknown);
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
            ReportOptionalAccess(
                target,
                syntax.IsOptional,
                true,
                syntax.OpenBracketToken.Span
            );
            BoundExpression index = BindExpression(syntax.Index, TypeSymbols.String);

            if (profile.OpenObjects != OpenObjectsFeature.Enabled)
            {
                ReportFeature(
                    DiagnosticCodes.DisabledOpenObjects,
                    syntax.OpenBracketToken.Span,
                    "Dynamic object element access is disabled by the language profile."
                );
            }

            return new BoundExpression.ElementAccess(
                syntax,
                TypeSymbols.Nullable(TypeSymbols.Unknown),
                target,
                index,
                null,
                true,
                true,
                syntax.IsOptional
            );
        }

        BindExpression(syntax.Index);

        if (target.Type.Kind != TypeKind.ErrorRecovery)
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

            if (scope.TryLookup(name, out BoundVariableSymbol? variable))
            {
                if (variable is UserParameterSymbol parameter)
                {
                    Report(
                        DiagnosticCodes.CannotAssignParameter,
                        syntax.Span,
                        $"Parameter '{name}' cannot be assigned."
                    );

                    return new BoundExpression.Parameter(nameSyntax, parameter);
                }

                return new BoundExpression.Local(
                    nameSyntax,
                    (LocalSymbol)variable
                );
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
            TokenKind.FloatKeyword => TypeSymbols.Float,
            TokenKind.NumberKeyword => TypeSymbols.Number,
            TokenKind.StringKeyword => TypeSymbols.String,
            TokenKind.PrimitiveKeyword => TypeSymbols.Primitive,
            TokenKind.ErrorKeyword => TypeSymbols.ErrorValue,
            TokenKind.UnknownKeyword => TypeSymbols.Unknown,
            TokenKind.ObjectKeyword => TypeSymbols.Object,
            TokenKind.VoidKeyword => TypeSymbols.Void,
            TokenKind.Identifier
                when environment.TryGetType(name, out ObjectTypeSymbol? objectType) =>
                objectType,
            _ => TypeSymbols.ErrorRecovery,
        };

        if (type.Kind == TypeKind.ErrorRecovery)
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

            if (type.Kind == TypeKind.Void)
            {
                Report(
                    DiagnosticCodes.InvalidReturnType,
                    syntax.Span,
                    "Type 'void' cannot have nullable or array suffixes."
                );
                return TypeSymbols.ErrorRecovery;
            }

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

                if (
                    index + 1 < syntax.SuffixTokens.Count &&
                    syntax.SuffixTokens[index + 1].Kind == TokenKind.Dollar
                )
                {
                    type = TypeSymbols.ReadOnlyArray(
                        ((ArrayTypeSymbol)type).ElementType
                    );
                    index++;
                }
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

    private static bool TryGetErrorProperty(
        string name,
        out ObjectPropertySymbol? property
    )
    {
        property = name switch
        {
            "code" or "category" or "message" =>
                new ObjectPropertySymbol(
                    name,
                    TypeSymbols.String,
                    false,
                    true
                ),
            "cause" =>
                new ObjectPropertySymbol(
                    name,
                    TypeSymbols.ErrorValue,
                    true,
                    true
                ),
            "data" =>
                new ObjectPropertySymbol(
                    name,
                    TypeSymbols.Nullable(TypeSymbols.Unknown),
                    true,
                    true
                ),
            "spanStart" or "spanLength" =>
                new ObjectPropertySymbol(
                    name,
                    TypeSymbols.Int,
                    false,
                    true
                ),
            _ => null,
        };

        return property is not null;
    }

    private void MergeBranches(
        FlowState target,
        FlowState thenState,
        FlowState elseState
    )
    {
        target.Assigned.Clear();
        target.PossiblyAssigned.Clear();
        target.ReadOnlyAssignments.Clear();

        if (thenState.CanCompleteNormally && elseState.CanCompleteNormally)
        {
            target.Assigned.UnionWith(thenState.Assigned);
            target.Assigned.IntersectWith(elseState.Assigned);
            target.PossiblyAssigned.UnionWith(thenState.PossiblyAssigned);
            target.PossiblyAssigned.UnionWith(elseState.PossiblyAssigned);
            target.ReadOnlyAssignments.UnionWith(thenState.ReadOnlyAssignments);
            target.ReadOnlyAssignments.UnionWith(elseState.ReadOnlyAssignments);
        }
        else if (thenState.CanCompleteNormally)
        {
            target.Assigned.UnionWith(thenState.Assigned);
            target.PossiblyAssigned.UnionWith(thenState.PossiblyAssigned);
            target.ReadOnlyAssignments.UnionWith(thenState.ReadOnlyAssignments);
        }
        else if (elseState.CanCompleteNormally)
        {
            target.Assigned.UnionWith(elseState.Assigned);
            target.PossiblyAssigned.UnionWith(elseState.PossiblyAssigned);
            target.ReadOnlyAssignments.UnionWith(elseState.ReadOnlyAssignments);
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
            result.PossiblyAssigned.UnionWith(state.PossiblyAssigned);
            result.ReadOnlyAssignments.UnionWith(state.ReadOnlyAssignments);
        }

        result.CanCompleteNormally = true;
        return result;
    }

    private static void ReplaceWithIntersection(
        FlowState target,
        IReadOnlyCollection<FlowState> states
    )
    {
        FlowState intersection = CreateIntersectionState(states);
        target.Assigned.Clear();
        target.Assigned.UnionWith(intersection.Assigned);
        target.PossiblyAssigned.Clear();
        target.PossiblyAssigned.UnionWith(intersection.PossiblyAssigned);
        target.ReadOnlyAssignments.Clear();
        target.ReadOnlyAssignments.UnionWith(intersection.ReadOnlyAssignments);
        target.CanCompleteNormally = true;
    }

    private static void ReplaceWithLoopExitStates(
        FlowState target,
        IReadOnlyCollection<FlowState> exitStates
    )
    {
        IReadOnlyCollection<FlowState> reachableExitStates =
        [
            .. exitStates.Where(static state => state.CanCompleteNormally),
        ];

        if (reachableExitStates.Count == 0)
        {
            target.CanCompleteNormally = false;
            return;
        }

        ReplaceWithIntersection(target, reachableExitStates);
    }

    private void ReportReadOnlyAssignmentsReachingBackEdge(
        FlowState loopEntryState,
        IEnumerable<FlowState> backEdgeStates,
        int currentLoopDepth
    )
    {
        foreach (FlowState backEdgeState in backEdgeStates)
        {
            if (!backEdgeState.CanCompleteNormally)
            {
                continue;
            }

            foreach (
                (LocalSymbol symbol, TextSpan span) in
                backEdgeState.ReadOnlyAssignments.Except(
                    loopEntryState.ReadOnlyAssignments
                )
            )
            {
                if (symbol.DeclarationLoopDepth >= currentLoopDepth)
                {
                    continue;
                }

                ReportReadOnlyAssignment(
                    symbol,
                    span,
                    $"Read-only local variable '{symbol.Name}' may be assigned again by another loop iteration."
                );
            }
        }
    }

    private void ReportReadOnlyAssignment(
        LocalSymbol symbol,
        TextSpan span,
        string message
    )
    {
        if (!readOnlyAssignmentDiagnostics.Add((span.Start, span.Length)))
        {
            return;
        }

        Report(
            DiagnosticCodes.CannotReassignReadOnlyLocal,
            span,
            message,
            DiagnosticCategory.ControlFlow
        );
    }

    private static void RemoveVariablesFromFlowState(
        FlowState state,
        IEnumerable<BoundVariableSymbol> variables
    )
    {
        foreach (BoundVariableSymbol variable in variables)
        {
            state.Assigned.Remove(variable);
            state.PossiblyAssigned.Remove(variable);

            if (variable is LocalSymbol local)
            {
                IReadOnlyCollection<(LocalSymbol Symbol, TextSpan Span)> assignments =
                [
                    .. state.ReadOnlyAssignments.Where(
                        assignment => ReferenceEquals(assignment.Symbol, local)
                    ),
                ];

                foreach (
                    (LocalSymbol Symbol, TextSpan Span) assignment in assignments
                )
                {
                    state.ReadOnlyAssignments.Remove(assignment);
                }
            }
        }
    }

    private static bool IsConstantTrue(BoundExpression expression)
    {
        return BoundExpressionFacts.TryGetTruthiness(expression, out bool value) && value;
    }

    private static bool IsConstantFalse(BoundExpression expression)
    {
        return BoundExpressionFacts.TryGetTruthiness(expression, out bool value) && !value;
    }

    private void ValidateEnvironmentCompatibility()
    {
        if (
            profile.OpenObjects != OpenObjectsFeature.Enabled &&
            environment.Types.Any(static type => type.IsOpen)
        )
        {
            ReportFeature(
                DiagnosticCodes.DisabledOpenObjects,
                new TextSpan(0, 0),
                "The environment contains an open structured type, but open objects are disabled by the language profile."
            );
        }
    }

    private void ReportRecursiveFunctions(
        IReadOnlyCollection<UserFunctionSymbol> functions
    )
    {
        IReadOnlyDictionary<string, ISet<string>> calls =
            new Dictionary<string, ISet<string>>(userFunctionCalls);
        IReadOnlyCollection<string> recursiveFunctionIds =
            UserFunctionRecursionAnalyzer.FindRecursiveFunctions(
                [ .. functions.Select(static function => function.Id) ],
                calls
            );
        IReadOnlyDictionary<string, UserFunctionSymbol> functionsById =
            functions.ToDictionary(
                static function => function.Id,
                StringComparer.Ordinal
            );

        foreach (string functionId in recursiveFunctionIds)
        {
            UserFunctionSymbol function = functionsById[functionId];
            ReportFeature(
                DiagnosticCodes.DisabledRecursion,
                function.Declaration.IdentifierToken.Span,
                $"Function '{function.Name}' participates in recursion, which is disabled by the language profile."
            );
        }
    }

    private void ReportDisabledLoopLevel(
        SyntaxToken? levelSignToken,
        SyntaxToken? levelToken
    )
    {
        if (
            levelToken is null ||
            profile.Loops == LoopFeatures.None ||
            profile.MultiLevelLoopControl == MultiLevelLoopControlFeature.Enabled
        )
        {
            return;
        }

        ReportFeature(
            DiagnosticCodes.DisabledMultiLevelLoopControl,
            (levelSignToken ?? levelToken).Span,
            "Explicit loop-control levels are disabled by the language profile."
        );
    }

    private void ReportDisabledTrailingComma(
        IReadOnlyCollection<SyntaxToken> commaTokens,
        bool hasTrailingComma
    )
    {
        if (
            profile.TrailingCommas == TrailingCommasFeature.Enabled ||
            commaTokens.Count == 0 ||
            !hasTrailingComma
        )
        {
            return;
        }

        SyntaxToken lastComma = commaTokens.Last();

        ReportFeature(
            DiagnosticCodes.DisabledTrailingCommas,
            lastComma.Span,
            "Trailing commas in literals are disabled by the language profile."
        );
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
            TypeSymbol operandType = GetNumericResultType(leftType, rightType);
            left = ConvertRequiredOperand(left, operandType);
            right = ConvertRequiredOperand(right, operandType);
            return;
        }

        if (operatorKind == TokenKind.Plus && resultType.Kind == TypeKind.String)
        {
            left = ConvertRequiredOperand(left, TypeSymbols.String);
            right = ConvertRequiredOperand(right, TypeSymbols.String);
            return;
        }

        if (
            operatorKind is
            TokenKind.LeftShift or
            TokenKind.RightShift
        )
        {
            left = ConvertRequiredOperand(left, TypeSymbols.Int);
            right = ConvertRequiredOperand(right, TypeSymbols.Int);
            return;
        }

        if (
            operatorKind is
            TokenKind.Ampersand or
            TokenKind.Caret or
            TokenKind.Pipe
        )
        {
            left = ConvertRequiredOperand(left, resultType);
            right = ConvertRequiredOperand(right, resultType);
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

        if (
            operatorKind is TokenKind.EqualEqual or TokenKind.BangEqual &&
            IsNumeric(leftType) &&
            IsNumeric(rightType)
        )
        {
            TypeSymbol operandType = GetNumericResultType(leftType, rightType);
            left = ConvertRequiredOperand(left, operandType);
            right = ConvertRequiredOperand(right, operandType);
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
                conversionKind,
                false
            );
    }

    private static BoundExpression ConvertImplicit(
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

        return conversionKind == ConversionKind.Implicit
            ? new BoundExpression.Conversion(
                expression.Syntax,
                targetType,
                expression,
                ConversionKind.Implicit,
                false
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

    private TypeSymbol? GetCommonType(TypeSymbol left, TypeSymbol right)
    {
        TypeSymbol? commonType = TypeRelations.GetCommonType(left, right);

        return profile.LanguageVersion < LanguageVersion.Version1_2 &&
            commonType is not null &&
            ContainsPrimitive(commonType)
                ? null
                : commonType;
    }

    private static bool ContainsPrimitive(TypeSymbol type)
    {
        return type switch
        {
            { Kind: TypeKind.Primitive } => true,
            NullableTypeSymbol nullable => ContainsPrimitive(nullable.UnderlyingType),
            ArrayTypeSymbol array => ContainsPrimitive(array.ElementType),
            _ => false,
        };
    }

    private static TypeSymbol GetNonNullable(TypeSymbol? type)
    {
        return type is NullableTypeSymbol nullable
            ? nullable.UnderlyingType
            : type ?? TypeSymbols.ErrorRecovery;
    }

    private static bool IsNumeric(TypeSymbol type)
    {
        return type.Kind is TypeKind.Int or TypeKind.Float or TypeKind.Number;
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
            TypeKind.Float or
            TypeKind.Number or
            TypeKind.String or
            TypeKind.Primitive or
            TypeKind.Null;
    }

    private static TypeSymbol GetNumericResultType(
        TypeSymbol left,
        TypeSymbol right
    )
    {
        if (left.Kind == TypeKind.Number || right.Kind == TypeKind.Number)
        {
            return TypeSymbols.Number;
        }

        return left.Kind == TypeKind.Float || right.Kind == TypeKind.Float
            ? TypeSymbols.Float
            : TypeSymbols.Int;
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

    private static bool IsPrefixedInteger(string text)
    {
        return text is [ '0', 'b' or 'B' or 'o' or 'O' or 'x' or 'X', .. ];
    }

    private static bool TryParsePrefixedInteger(
        string text,
        bool isNegative,
        out long value,
        out bool overflow
    )
    {
        int numberBase = text[1] switch
        {
            'b' or 'B' => 2,
            'o' or 'O' => 8,
            'x' or 'X' => 16,
            _ => throw new InvalidOperationException(),
        };
        ulong limit = isNegative
            ? 1UL << 63
            : long.MaxValue;
        ulong magnitude = 0;

        for (int index = 2; index < text.Length; index++)
        {
            int digit = text[index] switch
            {
                >= '0' and <= '9' => text[index] - '0',
                >= 'a' and <= 'f' => text[index] - 'a' + 10,
                >= 'A' and <= 'F' => text[index] - 'A' + 10,
                _ => -1,
            };

            if (digit < 0 || digit >= numberBase)
            {
                value = 0;
                overflow = false;
                return false;
            }

            if (magnitude > (limit - (uint)digit) / (uint)numberBase)
            {
                value = 0;
                overflow = true;
                return false;
            }

            magnitude = magnitude * (uint)numberBase + (uint)digit;
        }

        if (text.Length == 2)
        {
            value = 0;
            overflow = false;
            return false;
        }

        value = isNegative
            ? magnitude == 1UL << 63
                ? long.MinValue
                : -(long)magnitude
            : (long)magnitude;
        overflow = false;
        return true;
    }

    private string GetText(SyntaxToken token)
    {
        return token.Value ?? source.GetText(token.Span);
    }

    private void ReportConstantCondition(
        BoundExpression expression,
        TextSpan span
    )
    {
        if (
            expression.Type.Kind == TypeKind.ErrorRecovery ||
            !BoundExpressionFacts.TryGetTruthiness(expression, out bool value)
        )
        {
            return;
        }

        bool isTruthiness =
            expression is BoundExpression.Truthiness truthiness &&
            truthiness.Expression.Type.Kind != TypeKind.Bool;
        string description = isTruthiness
            ? value
                ? "truthy"
                : "falsy"
            : value
                ? "true"
                : "false";

        ReportWarning(
            DiagnosticCodes.ConstantCondition,
            span,
            $"Expression is always {description}.",
            DiagnosticCategory.ControlFlow
        );
    }

    private void ReportLogicalExpression(
        BoundExpression.Binary expression,
        SyntaxToken operatorToken
    )
    {
        if (
            operatorToken.Kind is not
            TokenKind.AmpersandAmpersand and not
            TokenKind.PipePipe
        )
        {
            return;
        }

        bool hasLeftValue = BoundExpressionFacts.TryGetTruthiness(
            expression.Left,
            out bool leftValue
        );
        bool hasRightValue = BoundExpressionFacts.TryGetTruthiness(
            expression.Right,
            out bool rightValue
        );
        bool isAnd = operatorToken.Kind == TokenKind.AmpersandAmpersand;

        if (hasLeftValue && (isAnd ? !leftValue : leftValue))
        {
            ReportWarning(
                DiagnosticCodes.ConstantCondition,
                expression.Span,
                $"Expression is always {(leftValue ? "true" : "false")}.",
                DiagnosticCategory.ControlFlow
            );
            return;
        }

        if (hasLeftValue && hasRightValue)
        {
            ReportWarning(
                DiagnosticCodes.ConstantCondition,
                expression.Span,
                $"Expression is always {(rightValue ? "true" : "false")}.",
                DiagnosticCategory.ControlFlow
            );
            return;
        }

        if (hasLeftValue)
        {
            ReportConstantLogicalOperand(
                expression,
                leftValue,
                "Left"
            );
            return;
        }

        if (hasRightValue && (isAnd ? rightValue : !rightValue))
        {
            ReportConstantLogicalOperand(
                expression,
                rightValue,
                "Right"
            );
        }
    }

    private void ReportConstantLogicalOperand(
        BoundExpression.Binary expression,
        bool value,
        string operandName
    )
    {
        ReportWarning(
            DiagnosticCodes.ConstantCondition,
            expression.Span,
            $"{operandName} operand is always {(value ? "true" : "false")}.",
            DiagnosticCategory.ControlFlow
        );
    }

    private void ReportConstantNullComparison(
        BoundExpression.Binary expression,
        SyntaxToken operatorToken
    )
    {
        if (
            operatorToken.Kind is not
                TokenKind.EqualEqual and not
                TokenKind.BangEqual and not
                TokenKind.EqualEqualEqual and not
                TokenKind.BangEqualEqual ||
            expression.Left.Type.Kind != TypeKind.Null &&
            expression.Right.Type.Kind != TypeKind.Null ||
            !BoundExpressionFacts.TryGetNullness(
                expression.Left,
                out bool leftIsNull
            ) ||
            !BoundExpressionFacts.TryGetNullness(
                expression.Right,
                out bool rightIsNull
            )
        )
        {
            return;
        }

        bool equal = leftIsNull == rightIsNull;
        bool value = operatorToken.Kind is
            TokenKind.EqualEqual or TokenKind.EqualEqualEqual
            ? equal
            : !equal;

        ReportWarning(
            DiagnosticCodes.ConstantNullComparison,
            expression.Span,
            $"Comparison with null is always {(value ? "true" : "false")}."
        );
    }

    private void ReportNullCoalescing(
        BoundExpression left,
        TextSpan span
    )
    {
        if (!BoundExpressionFacts.TryGetNullness(left, out bool isNull))
        {
            return;
        }

        ReportWarning(
            isNull
                ? DiagnosticCodes.NullCoalescingAlwaysUsesFallback
                : DiagnosticCodes.RedundantNullCoalescing,
            span,
            isNull
                ? "Null coalescing always uses the fallback operand."
                : "Null coalescing never uses the fallback operand."
        );
    }

    private void ReportOptionalAccess(
        BoundExpression target,
        bool isOptional,
        bool canBeAbsent,
        TextSpan operatorSpan
    )
    {
        if (
            !isOptional ||
            !BoundExpressionFacts.TryGetNullness(target, out bool isNull)
        )
        {
            return;
        }

        if (!isNull && canBeAbsent)
        {
            return;
        }

        ReportWarning(
            isNull
                ? DiagnosticCodes.OptionalAccessAlwaysNull
                : DiagnosticCodes.RedundantOptionalAccess,
            operatorSpan,
            isNull
                ? "Optional access target is always null."
                : "Optional access target is never null."
        );
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
        params IEnumerable<TypeSymbol> operandTypes
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

    private void ReportFeature(string code, TextSpan span, string message)
    {
        if (!featureDiagnostics.Add((code, span.Start, span.Length)))
        {
            return;
        }

        Report(code, span, message);
    }

    private void ReportWarning(
        string code,
        TextSpan span,
        string message,
        DiagnosticCategory category = DiagnosticCategory.Type
    )
    {
        if (!warningDiagnostics.Add((code, span.Start, span.Length)))
        {
            return;
        }

        diagnostics.Add(
            new Diagnostic(
                code,
                DiagnosticSeverity.Warning,
                category,
                span,
                message
            )
        );
    }
}
