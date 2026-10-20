using MuLang.Compiler.Syntax;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal static class SemanticDocumentBuilder
{
    public static IReadOnlyList<SemanticOccurrence> Build(BindingResult binding)
    {
        IList<SemanticOccurrence> occurrences = new List<SemanticOccurrence>();
        IDictionary<object, SemanticSymbolKey> localKeys =
            new Dictionary<object, SemanticSymbolKey>(
                ReferenceEqualityComparer.Instance
            );
        IDictionary<SemanticSymbolKey, TextSpan> definitions =
            new Dictionary<SemanticSymbolKey, TextSpan>();
        Stack<object> loopHighlightGroups = [ ];
        object? functionHighlightGroup = null;

        SemanticSymbolKey LocalKey(
            object identity,
            SemanticClassificationKind kind
        )
        {
            if (!localKeys.TryGetValue(identity, out SemanticSymbolKey? key))
            {
                key = new SemanticSymbolKey(kind, identity);
                localKeys.Add(identity, key);
            }

            return key;
        }

        static SemanticSymbolKey StableKey(
            SemanticClassificationKind kind,
            string category,
            string id
        )
        {
            string workspaceIdentity = $"{category}:{id.Length}:{id}";
            return new SemanticSymbolKey(
                kind,
                workspaceIdentity,
                workspaceIdentity
            );
        }

        SemanticSymbolKey? TypeKey(TypeSymbol type)
        {
            return type switch
            {
                ObjectTypeSymbol { Id: { } id } =>
                    StableKey(SemanticClassificationKind.Type, "type", id),
                ObjectTypeSymbol => LocalKey(
                    type,
                    SemanticClassificationKind.Type
                ),
                { Kind: not TypeKind.ErrorRecovery } when
                    IsIntrinsicType(type.Kind) =>
                    StableKey(
                        SemanticClassificationKind.Type,
                        "intrinsic-type",
                        type.Kind.ToString()
                    ),
                _ => null,
            };
        }

        SemanticSymbolKey PropertyKey(
            ObjectPropertySymbol property,
            ObjectTypeSymbol? owner
        )
        {
            while (
                binding.ContextualProperties.TryGetValue(
                    property,
                    out ObjectPropertySymbol? contextualProperty
                )
            )
            {
                property = contextualProperty;
                owner = binding.PropertyOwners[property];
            }

            return binding.WellKnownErrorProperties.Contains(property)
                ? BuiltInPropertyKey("error", property.Name)
                : owner?.Id is { } ownerId
                    ? StableKey(
                        SemanticClassificationKind.Property,
                        $"property:{ownerId.Length}:{ownerId}",
                        property.Name
                    )
                    : LocalKey(property, SemanticClassificationKind.Property);
        }

        static SemanticSymbolKey BuiltInPropertyKey(string owner, string name)
        {
            return StableKey(
                SemanticClassificationKind.Property,
                $"builtin-property:{owner}",
                name
            );
        }

        void Define(SemanticSymbolKey? symbol, TextSpan span)
        {
            if (symbol is not null)
            {
                definitions.TryAdd(symbol, span);
            }
        }

        void Add(
            SemanticSymbolKey? symbol,
            SemanticClassificationKind kind,
            SemanticOccurrenceRole role,
            TextSpan span,
            IReadOnlyList<SemanticHoverPart>? hoverParts = null,
            SemanticClassificationModifiers modifiers =
                SemanticClassificationModifiers.None,
            bool isClassification = true,
            SemanticHoverCategory? hoverCategory = null,
            IReadOnlyList<SemanticHoverItem>? hoverItems = null,
            object? highlightGroup = null,
            IReadOnlyList<TextSpan>? additionalHoverSpans = null
        )
        {
            TextSpan definitionSpan = default;
            bool hasDefinition =
                symbol is not null &&
                definitions.TryGetValue(symbol, out definitionSpan);
            IReadOnlyList<SemanticHoverItem> items = hoverItems ??
            (
                hoverParts is null
                    ? [ ]
                    :
                    [
                        new SemanticHoverItem(
                            hoverCategory ?? GetHoverCategory(kind),
                            hoverParts
                        ),
                    ]
            );
            occurrences.Add(
                new SemanticOccurrence(
                    symbol,
                    kind,
                    role,
                    modifiers,
                    span,
                    items,
                    hasDefinition ? definitionSpan : null,
                    isClassification,
                    highlightGroup,
                    additionalHoverSpans
                )
            );
        }

        static SemanticHoverCategory GetHoverCategory(
            SemanticClassificationKind kind
        )
        {
            return kind switch
            {
                SemanticClassificationKind.Variable =>
                    SemanticHoverCategory.Variable,
                SemanticClassificationKind.Parameter =>
                    SemanticHoverCategory.Parameter,
                SemanticClassificationKind.Function =>
                    SemanticHoverCategory.Function,
                SemanticClassificationKind.Type =>
                    SemanticHoverCategory.Type,
                SemanticClassificationKind.Property =>
                    SemanticHoverCategory.Property,
                SemanticClassificationKind.Literal =>
                    SemanticHoverCategory.Literal,
                SemanticClassificationKind.Operator =>
                    SemanticHoverCategory.Operator,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }

        void AddDeclaration(
            SemanticSymbolKey? symbol,
            SemanticClassificationKind kind,
            TextSpan span,
            IReadOnlyList<SemanticHoverPart>? hoverParts,
            SemanticClassificationModifiers modifiers,
            IReadOnlyList<TextSpan>? additionalHoverSpans = null
        )
        {
            if (symbol?.IsWorkspaceStable != true)
            {
                Define(symbol, span);
            }

            Add(
                symbol,
                kind,
                SemanticOccurrenceRole.Declaration,
                span,
                hoverParts,
                modifiers | SemanticClassificationModifiers.Declaration,
                additionalHoverSpans: additionalHoverSpans
            );
        }

        static IReadOnlyList<TextSpan> ModifierSpans(
            SyntaxToken? dollar,
            SyntaxToken? question = null
        )
        {
            IList<TextSpan> spans = new List<TextSpan>();

            if (dollar is not null)
            {
                spans.Add(dollar.Span);
            }

            if (question is not null)
            {
                spans.Add(question.Span);
            }

            return [ .. spans ];
        }

        string Source(TextSpan span)
        {
            return binding.Source.GetText(span);
        }

        IReadOnlyList<SemanticHoverPart> ExpressionDisplay(BoundExpression expression)
        {
            return SymbolDisplay.Expression(
                Source(expression.Span),
                expression.Type,
                expression.Syntax is LiteralExpressionSyntax literal
                    ? literal.LiteralToken.Kind switch
                    {
                        TokenKind.StringLiteral => SemanticHoverPartKind.String,
                        TokenKind.IntegerLiteral or TokenKind.NumberLiteral => SemanticHoverPartKind.Number,
                        _ => SemanticHoverPartKind.Keyword,
                    }
                    : SemanticHoverPartKind.Text
            );
        }

        void AddOperator(
            SyntaxToken token,
            IReadOnlyList<TypeSymbol> operands,
            TypeSymbol result,
            string? symbol = null
        )
        {
            Add(
                null,
                SemanticClassificationKind.Operator,
                SemanticOccurrenceRole.Reference,
                token.Span,
                SymbolDisplay.Operator(
                    symbol ?? Source(token.Span),
                    operands,
                    result
                ),
                isClassification: false
            );
        }

        void AddConditionalOperators(
            ConditionalExpressionSyntax syntax,
            IReadOnlyList<TypeSymbol> operands,
            TypeSymbol result
        )
        {
            AddOperator(
                syntax.QuestionToken,
                operands,
                result,
                "?:"
            );
            AddOperator(
                syntax.ColonToken,
                operands,
                result,
                "?:"
            );
        }

        void AddControlKeyword(SyntaxToken token, object group)
        {
            Add(
                null,
                SemanticClassificationKind.ControlFlow,
                SemanticOccurrenceRole.Reference,
                token.Span,
                isClassification: false,
                highlightGroup: group
            );
        }

        void VisitType(TypeSyntax? syntax)
        {
            if (syntax is null)
            {
                return;
            }

            switch (syntax.Primary)
            {
                case NamedTypeSyntax named:
                {
                    binding.Types.TryGetValue(named, out TypeSymbol? type);
                    SemanticSymbolKey? symbol = type is null
                        ? null
                        : TypeKey(type);
                    Add(
                        symbol,
                        SemanticClassificationKind.Type,
                        SemanticOccurrenceRole.Reference,
                        named.NameToken.Span,
                        type?.Kind == TypeKind.ErrorRecovery
                            ? null
                            : type is null
                                ? null
                                : SymbolDisplay.TypeDeclaration(type),
                        isClassification:
                        named.NameToken.Kind == TokenKind.Identifier
                    );
                    break;
                }

                case ObjectTypeBodySyntax body:
                {
                    VisitObjectTypeBody(body);
                    break;
                }
            }
        }

        void VisitObjectTypeBody(ObjectTypeBodySyntax body)
        {
            ObjectTypeSymbol? owner =
                binding.Types.GetValueOrDefault(body) as ObjectTypeSymbol;

            foreach (ObjectTypePropertySyntax propertySyntax in body.Properties)
            {
                binding.Properties.TryGetValue(
                    propertySyntax,
                    out ObjectPropertySymbol? property
                );
                SemanticSymbolKey? symbol = property is null
                    ? null
                    : PropertyKey(property, owner);
                AddDeclaration(
                    symbol,
                    SemanticClassificationKind.Property,
                    propertySyntax.NameToken.Span,
                    property is null ? null : SymbolDisplay.Property(property),
                    SemanticClassificationModifiers.None,
                    ModifierSpans(
                        propertySyntax.DollarToken,
                        propertySyntax.QuestionToken
                    )
                );
                VisitType(propertySyntax.Type);
            }
        }

        void VisitIfStatement(
            BoundStatement.If conditional,
            object? existingGroup = null
        )
        {
            object group = existingGroup ?? new object();
            IfStatementSyntax syntax =
                (IfStatementSyntax)conditional.Syntax;
            AddControlKeyword(syntax.IfKeyword, group);
            VisitExpression(
                conditional.Condition,
                SemanticOccurrenceRole.Read
            );
            VisitStatement(conditional.Then);

            if (conditional.Else is null)
            {
                return;
            }

            if (syntax.ElseKeyword is not null)
            {
                AddControlKeyword(syntax.ElseKeyword, group);
            }

            if (
                conditional.Else is BoundStatement.If nested &&
                syntax.ElseStatement is IfStatementSyntax
            )
            {
                VisitIfStatement(nested, group);
                return;
            }

            VisitStatement(conditional.Else);
        }

        void VisitStatement(BoundStatement statement)
        {
            switch (statement)
            {
                case BoundStatement.Block block:
                {
                    foreach (BoundStatement child in block.Statements)
                    {
                        VisitStatement(child);
                    }

                    break;
                }

                case BoundStatement.VariableDeclaration declaration:
                {
                    VariableDeclarationStatementSyntax syntax =
                        (VariableDeclarationStatementSyntax)declaration.Syntax;
                    SemanticSymbolKey symbol = LocalKey(
                        declaration.Local,
                        SemanticClassificationKind.Variable
                    );
                    AddDeclaration(
                        symbol,
                        SemanticClassificationKind.Variable,
                        syntax.IdentifierToken.Span,
                        SymbolDisplay.Variable(declaration.Local),
                        declaration.Local.IsReadOnly
                            ? SemanticClassificationModifiers.ReadOnly
                            : SemanticClassificationModifiers.None,
                        ModifierSpans(syntax.DollarToken)
                    );
                    VisitType(syntax.Type);

                    if (declaration.Initializer is not null)
                    {
                        VisitExpression(
                            declaration.Initializer,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundStatement.Assignment assignment:
                {
                    VisitExpression(
                        assignment.Target,
                        SemanticOccurrenceRole.Write
                    );
                    VisitExpression(
                        assignment.Value,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundStatement.Removal removal:
                {
                    VisitExpression(
                        removal.Target,
                        SemanticOccurrenceRole.Write
                    );
                    break;
                }

                case BoundStatement.ExpressionStatement expression:
                {
                    VisitExpression(
                        expression.Value,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundStatement.If conditional:
                {
                    VisitIfStatement(conditional);
                    break;
                }

                case BoundStatement.While loop:
                {
                    object group = new ();
                    AddControlKeyword(
                        ((WhileStatementSyntax)loop.Syntax).WhileKeyword,
                        group
                    );
                    VisitExpression(
                        loop.Condition,
                        SemanticOccurrenceRole.Read
                    );
                    loopHighlightGroups.Push(group);
                    VisitStatement(loop.Body);
                    loopHighlightGroups.Pop();
                    break;
                }

                case BoundStatement.For loop:
                {
                    object group = new ();
                    AddControlKeyword(
                        ((ForStatementSyntax)loop.Syntax).ForKeyword,
                        group
                    );

                    if (loop.Initializer is not null)
                    {
                        VisitStatement(loop.Initializer);
                    }

                    if (loop.Condition is not null)
                    {
                        VisitExpression(
                            loop.Condition,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    if (loop.Iterator is not null)
                    {
                        VisitStatement(loop.Iterator);
                    }

                    loopHighlightGroups.Push(group);
                    VisitStatement(loop.Body);
                    loopHighlightGroups.Pop();
                    break;
                }

                case BoundStatement.Break loopExit:
                {
                    if (
                        loopExit.Level >= 1 &&
                        loopExit.Level <= loopHighlightGroups.Count
                    )
                    {
                        AddControlKeyword(
                            ((BreakStatementSyntax)loopExit.Syntax)
                            .BreakKeyword,
                            loopHighlightGroups.ElementAt(
                                loopExit.Level - 1
                            )
                        );
                    }

                    break;
                }

                case BoundStatement.Continue iteration:
                {
                    if (
                        iteration.Level >= 1 &&
                        iteration.Level <= loopHighlightGroups.Count
                    )
                    {
                        AddControlKeyword(
                            ((ContinueStatementSyntax)iteration.Syntax)
                            .ContinueKeyword,
                            loopHighlightGroups.ElementAt(
                                iteration.Level - 1
                            )
                        );
                    }

                    break;
                }

                case BoundStatement.Return { Value: not null } result:
                {
                    if (functionHighlightGroup is not null)
                    {
                        AddControlKeyword(
                            ((ReturnStatementSyntax)result.Syntax)
                            .ReturnKeyword,
                            functionHighlightGroup
                        );
                    }

                    VisitExpression(
                        result.Value,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundStatement.Return result:
                {
                    if (functionHighlightGroup is not null)
                    {
                        AddControlKeyword(
                            ((ReturnStatementSyntax)result.Syntax)
                            .ReturnKeyword,
                            functionHighlightGroup
                        );
                    }

                    break;
                }

                case BoundStatement.Throw thrown:
                {
                    if (functionHighlightGroup is not null)
                    {
                        AddControlKeyword(
                            ((ThrowStatementSyntax)thrown.Syntax)
                            .ThrowKeyword,
                            functionHighlightGroup
                        );
                    }

                    VisitExpression(
                        thrown.Error,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundStatement.Try guarded:
                {
                    object group = new ();
                    TryStatementSyntax syntax =
                        (TryStatementSyntax)guarded.Syntax;
                    AddControlKeyword(syntax.TryKeyword, group);

                    if (syntax.CatchClause is { } catchClause)
                    {
                        AddControlKeyword(
                            catchClause.CatchKeyword,
                            group
                        );
                    }

                    if (syntax.FinallyClause is { } finallyClause)
                    {
                        AddControlKeyword(
                            finallyClause.FinallyKeyword,
                            group
                        );
                    }

                    VisitStatement(guarded.Protected);

                    if (guarded.ErrorLocal is not null)
                    {
                        SyntaxToken? identifier =
                            syntax.CatchClause?.IdentifierToken;

                        if (identifier is not null)
                        {
                            SemanticSymbolKey symbol = LocalKey(
                                guarded.ErrorLocal,
                                SemanticClassificationKind.Variable
                            );
                            AddDeclaration(
                                symbol,
                                SemanticClassificationKind.Variable,
                                identifier.Span,
                                SymbolDisplay.Variable(guarded.ErrorLocal),
                                SemanticClassificationModifiers.ReadOnly
                            );
                        }
                    }

                    if (guarded.Handler is not null)
                    {
                        VisitStatement(guarded.Handler);
                    }

                    if (guarded.Cleanup is not null)
                    {
                        VisitStatement(guarded.Cleanup);
                    }

                    break;
                }
            }
        }

        void VisitExpression(
            BoundExpression expression,
            SemanticOccurrenceRole role
        )
        {
            switch (expression)
            {
                case BoundExpression.Error
                {
                    Syntax: NameExpressionSyntax syntax,
                }:
                {
                    Add(
                        null,
                        SemanticClassificationKind.Variable,
                        role,
                        syntax.IdentifierToken.Span,
                        SymbolDisplay.Unresolved(
                            binding.Source.GetText(
                                syntax.IdentifierToken.Span
                            )
                        ),
                        isClassification: false
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: CallExpressionSyntax syntax,
                } error:
                {
                    if (syntax.Target is NameExpressionSyntax target)
                    {
                        Add(
                            null,
                            SemanticClassificationKind.Function,
                            SemanticOccurrenceRole.Reference,
                            target.IdentifierToken.Span,
                            SymbolDisplay.UnresolvedFunction(
                                Source(target.IdentifierToken.Span)
                            ),
                            isClassification: false
                        );
                    }

                    foreach (BoundExpression child in error.Children)
                    {
                        VisitExpression(
                            child,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: MemberAccessExpressionSyntax syntax,
                } error:
                {
                    foreach (BoundExpression child in error.Children)
                    {
                        VisitExpression(
                            child,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    Add(
                        null,
                        SemanticClassificationKind.Property,
                        role,
                        syntax.NameToken.Span,
                        SymbolDisplay.Unresolved(
                            Source(syntax.NameToken.Span),
                            SemanticHoverPartKind.Property
                        ),
                        isClassification: false
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: ElementAccessExpressionSyntax,
                    Children: [ { } target, { } key ],
                }:
                {
                    VisitExpression(
                        target,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        key,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: UnaryExpressionSyntax syntax,
                    Children: [ { } operand ],
                }:
                {
                    VisitExpression(
                        operand,
                        SemanticOccurrenceRole.Read
                    );
                    AddOperator(
                        syntax.OperatorToken,
                        [ operand.Type ],
                        TypeSymbols.ErrorRecovery
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: BinaryExpressionSyntax syntax,
                    Children:
                    [
                        { } left,
                        { } right,
                    ],
                } when IsHoverOperator(syntax.OperatorToken.Kind):
                {
                    VisitExpression(
                        left,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        right,
                        SemanticOccurrenceRole.Read
                    );
                    AddOperator(
                        syntax.OperatorToken,
                        [ left.Type, right.Type ],
                        TypeSymbols.ErrorRecovery
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: BinaryExpressionSyntax
                    {
                        OperatorToken.Kind: TokenKind.QuestionQuestion,
                    } syntax,
                    Children:
                    [
                        { } left,
                        { } right,
                    ],
                }:
                {
                    VisitExpression(
                        left,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        right,
                        SemanticOccurrenceRole.Read
                    );
                    AddOperator(
                        syntax.OperatorToken,
                        [ left.Type, right.Type ],
                        TypeSymbols.ErrorRecovery
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: ConditionalExpressionSyntax syntax,
                    Children:
                    [
                        { } condition,
                        { } whenTrue,
                        { } whenFalse,
                    ],
                }:
                {
                    VisitExpression(
                        condition,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        whenTrue,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        whenFalse,
                        SemanticOccurrenceRole.Read
                    );
                    AddConditionalOperators(
                        syntax,
                        [
                            condition.Type,
                            whenTrue.Type,
                            whenFalse.Type,
                        ],
                        TypeSymbols.ErrorRecovery
                    );
                    break;
                }

                case BoundExpression.Error error:
                {
                    foreach (BoundExpression child in error.Children)
                    {
                        VisitExpression(
                            child,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundExpression.Literal:
                {
                    Add(
                        null,
                        SemanticClassificationKind.Literal,
                        SemanticOccurrenceRole.Reference,
                        expression.Span,
                        ExpressionDisplay(expression),
                        isClassification: false
                    );
                    break;
                }

                case BoundExpression.Local local:
                {
                    Add(
                        LocalKey(
                            local.Symbol,
                            SemanticClassificationKind.Variable
                        ),
                        SemanticClassificationKind.Variable,
                        role,
                        local.Span,
                        SymbolDisplay.Variable(local.Symbol),
                        local.Symbol.IsReadOnly
                            ? SemanticClassificationModifiers.ReadOnly
                            : SemanticClassificationModifiers.None
                    );
                    break;
                }

                case BoundExpression.Parameter parameter:
                {
                    Add(
                        LocalKey(
                            parameter.Symbol,
                            SemanticClassificationKind.Parameter
                        ),
                        SemanticClassificationKind.Parameter,
                        role,
                        parameter.Span,
                        SymbolDisplay.Parameter(parameter.Symbol)
                    );
                    break;
                }

                case BoundExpression.Global global:
                {
                    Add(
                        StableKey(
                            SemanticClassificationKind.Variable,
                            "global",
                            global.Symbol.Id
                        ),
                        SemanticClassificationKind.Variable,
                        role,
                        global.Span,
                        SymbolDisplay.Variable(global.Symbol),
                        SemanticClassificationModifiers.ReadOnly |
                        SemanticClassificationModifiers.DefaultLibrary
                    );
                    break;
                }

                case BoundExpression.Array array:
                {
                    foreach (BoundExpression element in array.Elements)
                    {
                        VisitExpression(
                            element,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundExpression.Object value:
                {
                    ObjectLiteralExpressionSyntax syntax =
                        (ObjectLiteralExpressionSyntax)value.Syntax;

                    foreach (
                        ObjectPropertyInitializerSyntax propertySyntax
                        in syntax.Properties
                    )
                    {
                        binding.Properties.TryGetValue(
                            propertySyntax,
                            out ObjectPropertySymbol? property
                        );
                        ObjectTypeSymbol owner =
                            property is not null &&
                            binding.PropertyOwners.TryGetValue(
                                property,
                                out ObjectTypeSymbol? propertyOwner
                            )
                                ? propertyOwner
                                : value.ObjectType;
                        SemanticSymbolKey? symbol = property is null
                            ? null
                            : PropertyKey(property, owner);
                        IReadOnlyList<SemanticHoverPart>? content = property is null
                            ? null
                            : SymbolDisplay.Property(property);

                        if (
                            symbol is null ||
                            !symbol.IsWorkspaceStable &&
                            !definitions.ContainsKey(symbol)
                        )
                        {
                            AddDeclaration(
                                symbol,
                                SemanticClassificationKind.Property,
                                propertySyntax.NameToken.Span,
                                content,
                                SemanticClassificationModifiers.None,
                                ModifierSpans(
                                    propertySyntax.DollarToken,
                                    propertySyntax.QuestionToken
                                )
                            );
                        }
                        else
                        {
                            Add(
                                symbol,
                                SemanticClassificationKind.Property,
                                SemanticOccurrenceRole.Write,
                                propertySyntax.NameToken.Span,
                                content,
                                SemanticClassificationModifiers.Declaration,
                                additionalHoverSpans: ModifierSpans(
                                    propertySyntax.DollarToken,
                                    propertySyntax.QuestionToken
                                )
                            );
                        }

                        VisitType(propertySyntax.Type);
                    }

                    foreach (
                        BoundExpression.ObjectProperty property
                        in value.Properties
                    )
                    {
                        VisitExpression(
                            property.Value,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundExpression.Unary unary:
                {
                    VisitExpression(
                        unary.Operand,
                        SemanticOccurrenceRole.Read
                    );
                    AddOperator(
                        ((UnaryExpressionSyntax)unary.Syntax)
                        .OperatorToken,
                        [ unary.Operand.Type ],
                        unary.Type
                    );
                    break;
                }

                case BoundExpression.Binary binary:
                {
                    VisitExpression(
                        binary.Left,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        binary.Right,
                        SemanticOccurrenceRole.Read
                    );
                    AddOperator(
                        ((BinaryExpressionSyntax)binary.Syntax)
                        .OperatorToken,
                        [ binary.Left.Type, binary.Right.Type ],
                        binary.Type
                    );
                    break;
                }

                case BoundExpression.Coalescing coalescing:
                {
                    VisitExpression(
                        coalescing.Left,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        coalescing.Right,
                        SemanticOccurrenceRole.Read
                    );
                    AddOperator(
                        ((BinaryExpressionSyntax)coalescing.Syntax)
                        .OperatorToken,
                        [
                            coalescing.Left.Type,
                            coalescing.Right.Type,
                        ],
                        coalescing.Type
                    );
                    break;
                }

                case BoundExpression.Conversion conversion:
                {
                    VisitExpression(
                        conversion.Expression,
                        SemanticOccurrenceRole.Read
                    );

                    if (conversion.Syntax is ConversionExpressionSyntax syntax)
                    {
                        VisitType(syntax.Type);
                    }

                    break;
                }

                case BoundExpression.Truthiness truthiness:
                {
                    VisitExpression(
                        truthiness.Expression,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundExpression.TypeTest typeTest:
                {
                    VisitExpression(
                        typeTest.Expression,
                        SemanticOccurrenceRole.Read
                    );
                    VisitType(
                        ((TypeTestExpressionSyntax)typeTest.Syntax).Type
                    );
                    break;
                }

                case BoundExpression.PropertyTest propertyTest:
                {
                    VisitExpression(
                        propertyTest.Target,
                        SemanticOccurrenceRole.Read
                    );

                    if (
                        propertyTest.Property is not null &&
                        propertyTest.Key is BoundExpression.Literal
                    )
                    {
                        ObjectTypeSymbol? owner = GetObjectType(
                            propertyTest.Target.Type
                        );
                        Add(
                            PropertyKey(propertyTest.Property, owner),
                            SemanticClassificationKind.Property,
                            SemanticOccurrenceRole.Read,
                            propertyTest.Key.Span,
                            SymbolDisplay.Property(propertyTest.Property)
                        );
                    }
                    else
                    {
                        VisitExpression(
                            propertyTest.Key,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundExpression.Conditional conditional:
                {
                    VisitExpression(
                        conditional.Condition,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        conditional.WhenTrue,
                        SemanticOccurrenceRole.Read
                    );
                    VisitExpression(
                        conditional.WhenFalse,
                        SemanticOccurrenceRole.Read
                    );
                    AddConditionalOperators(
                        (ConditionalExpressionSyntax)conditional.Syntax,
                        [
                            conditional.Condition.Type,
                            conditional.WhenTrue.Type,
                            conditional.WhenFalse.Type,
                        ],
                        conditional.Type
                    );
                    break;
                }

                case BoundExpression.ProviderCall call:
                {
                    Add(
                        StableKey(
                            SemanticClassificationKind.Function,
                            "function",
                            call.Function.Id
                        ),
                        SemanticClassificationKind.Function,
                        SemanticOccurrenceRole.Reference,
                        ((CallExpressionSyntax)call.Syntax).Target.Span,
                        SymbolDisplay.Function(call.Function),
                        SemanticClassificationModifiers.DefaultLibrary
                    );

                    foreach (BoundExpression argument in call.Arguments)
                    {
                        VisitExpression(
                            argument,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundExpression.UserCall call:
                {
                    Add(
                        LocalKey(
                            call.Function,
                            SemanticClassificationKind.Function
                        ),
                        SemanticClassificationKind.Function,
                        SemanticOccurrenceRole.Reference,
                        ((CallExpressionSyntax)call.Syntax).Target.Span,
                        SymbolDisplay.Function(call.Function)
                    );

                    foreach (BoundExpression argument in call.Arguments)
                    {
                        VisitExpression(
                            argument,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }

                case BoundExpression.MemberAccess member:
                {
                    VisitExpression(
                        member.Target,
                        SemanticOccurrenceRole.Read
                    );
                    TextSpan span =
                        ((MemberAccessExpressionSyntax)member.Syntax)
                        .NameToken
                        .Span;
                    ObjectTypeSymbol? owner = GetObjectType(member.Target.Type);
                    ObjectPropertySymbol? property = member.Property;
                    SemanticSymbolKey? symbol;
                    IReadOnlyList<SemanticHoverPart>? content;

                    if (property is not null)
                    {
                        symbol = GetNonNullable(member.Target.Type).Kind ==
                            TypeKind.ErrorValue
                                ? BuiltInPropertyKey("error", property.Name)
                                : PropertyKey(property, owner);
                        content = SymbolDisplay.Property(property);
                    }
                    else if (member.IsArrayLength)
                    {
                        symbol = BuiltInPropertyKey("array", "length");
                        content = SymbolDisplay.Property(
                            new ObjectPropertySymbol(
                                "length",
                                member.Type,
                                isReadOnly: true
                            )
                        );
                    }
                    else if (member.IsDynamic)
                    {
                        symbol = null;
                        content = SymbolDisplay.Expression(
                            member.Name,
                            member.Type,
                            SemanticHoverPartKind.Property
                        );
                    }
                    else
                    {
                        symbol = null;
                        content = null;
                    }

                    Add(
                        symbol,
                        SemanticClassificationKind.Property,
                        role,
                        span,
                        content
                    );
                    break;
                }

                case BoundExpression.ElementAccess element:
                {
                    VisitExpression(
                        element.Target,
                        SemanticOccurrenceRole.Read
                    );

                    if (
                        element.Property is not null &&
                        element.Index is BoundExpression.Literal
                    )
                    {
                        ObjectTypeSymbol? owner = GetObjectType(
                            element.Target.Type
                        );
                        SemanticSymbolKey symbol =
                            GetNonNullable(element.Target.Type).Kind ==
                            TypeKind.ErrorValue
                                ? BuiltInPropertyKey(
                                    "error",
                                    element.Property.Name
                                )
                                : PropertyKey(element.Property, owner);
                        Add(
                            symbol,
                            SemanticClassificationKind.Property,
                            role,
                            element.Index.Span,
                            SymbolDisplay.Property(element.Property)
                        );
                    }
                    else
                    {
                        VisitExpression(
                            element.Index,
                            SemanticOccurrenceRole.Read
                        );
                    }

                    break;
                }
            }
        }

        if (binding.Root is BoundRoot.Program predeclaredProgram)
        {
            ProgramRootSyntax root =
                (ProgramRootSyntax)predeclaredProgram.Syntax;

            foreach (TypeDeclarationSyntax declaration in root.Types)
            {
                if (
                    binding.Types.TryGetValue(
                        declaration,
                        out TypeSymbol? type
                    )
                )
                {
                    Define(TypeKey(type), declaration.IdentifierToken.Span);
                }
            }

            foreach (BoundFunction function in predeclaredProgram.Functions)
            {
                Define(
                    LocalKey(
                        function.Symbol,
                        SemanticClassificationKind.Function
                    ),
                    function.Symbol.Declaration.IdentifierToken.Span
                );
            }
        }

        switch (binding.Root)
        {
            case BoundRoot.Expression expression:
            {
                VisitExpression(
                    expression.Value,
                    SemanticOccurrenceRole.Read
                );
                break;
            }

            case BoundRoot.Program program:
            {
                ProgramRootSyntax root = (ProgramRootSyntax)program.Syntax;

                foreach (TypeDeclarationSyntax declaration in root.Types)
                {
                    binding.Types.TryGetValue(
                        declaration,
                        out TypeSymbol? type
                    );
                    SemanticSymbolKey? symbol = type is null
                        ? null
                        : TypeKey(type);
                    AddDeclaration(
                        symbol,
                        SemanticClassificationKind.Type,
                        declaration.IdentifierToken.Span,
                        type is null
                            ? null
                            : SymbolDisplay.TypeDeclaration(type),
                        SemanticClassificationModifiers.None
                    );
                    VisitObjectTypeBody(declaration.Body);
                }

                foreach (BoundFunction function in program.Functions)
                {
                    FunctionDeclarationSyntax declaration =
                        function.Symbol.Declaration;
                    object group = new ();
                    AddControlKeyword(
                        declaration.FuncKeyword,
                        group
                    );
                    SemanticSymbolKey functionKey = LocalKey(
                        function.Symbol,
                        SemanticClassificationKind.Function
                    );
                    AddDeclaration(
                        functionKey,
                        SemanticClassificationKind.Function,
                        declaration.IdentifierToken.Span,
                        SymbolDisplay.Function(function.Symbol),
                        SemanticClassificationModifiers.None
                    );
                    VisitType(declaration.ReturnType);

                    for (
                        int index = 0;
                        index < function.Symbol.Parameters.Count;
                        index++
                    )
                    {
                        ParameterSyntax parameter =
                            declaration.Parameters[index];
                        UserParameterSymbol parameterSymbol =
                            function.Symbol.Parameters[index];
                        AddDeclaration(
                            LocalKey(
                                parameterSymbol,
                                SemanticClassificationKind.Parameter
                            ),
                            SemanticClassificationKind.Parameter,
                            parameter.IdentifierToken.Span,
                            SymbolDisplay.Parameter(parameterSymbol),
                            SemanticClassificationModifiers.None
                        );
                        VisitType(parameter.Type);
                    }

                    object? previousFunctionGroup =
                        functionHighlightGroup;
                    functionHighlightGroup = group;

                    foreach (BoundStatement statement in function.Statements)
                    {
                        VisitStatement(statement);
                    }

                    functionHighlightGroup =
                        previousFunctionGroup;
                }

                foreach (BoundStatement statement in program.Statements)
                {
                    VisitStatement(statement);
                }

                break;
            }
        }

        return
        [
            .. occurrences
                .OrderBy(static occurrence => occurrence.Span.Start)
                .ThenBy(static occurrence => occurrence.Span.Length),
        ];

        static ObjectTypeSymbol? GetObjectType(TypeSymbol type)
        {
            return type switch
            {
                ObjectTypeSymbol objectType => objectType,
                NullableTypeSymbol { UnderlyingType: ObjectTypeSymbol objectType } =>
                    objectType,
                _ => null,
            };
        }

        static TypeSymbol GetNonNullable(TypeSymbol type)
        {
            return type is NullableTypeSymbol nullable
                ? nullable.UnderlyingType
                : type;
        }

        static bool IsHoverOperator(TokenKind kind)
        {
            return kind is
                TokenKind.Plus or
                TokenKind.Minus or
                TokenKind.Asterisk or
                TokenKind.Slash or
                TokenKind.Percent or
                TokenKind.LessThan or
                TokenKind.LessThanOrEqual or
                TokenKind.GreaterThan or
                TokenKind.GreaterThanOrEqual or
                TokenKind.EqualEqual or
                TokenKind.EqualEqualEqual or
                TokenKind.BangEqual or
                TokenKind.BangEqualEqual or
                TokenKind.Ampersand or
                TokenKind.AmpersandAmpersand or
                TokenKind.Caret or
                TokenKind.Pipe or
                TokenKind.PipePipe or
                TokenKind.LeftShift or
                TokenKind.RightShift or
                TokenKind.Bang or
                TokenKind.Tilde;
        }

        static bool IsIntrinsicType(TypeKind kind)
        {
            return kind is
                TypeKind.Bool or
                TypeKind.Int or
                TypeKind.Float or
                TypeKind.Number or
                TypeKind.String or
                TypeKind.Primitive or
                TypeKind.ErrorValue or
                TypeKind.Unknown or
                TypeKind.Object or
                TypeKind.Void;
        }
    }
}
