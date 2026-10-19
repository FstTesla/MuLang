using MuLang.Compiler.Syntax;
using MuLang.Core.Symbols;
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
            string? hoverContent = null,
            SemanticClassificationModifiers modifiers =
                SemanticClassificationModifiers.None,
            bool isClassification = true,
            SemanticHoverCategory? hoverCategory = null,
            IReadOnlyList<SemanticHoverItem>? hoverItems = null
        )
        {
            TextSpan definitionSpan = default;
            bool hasDefinition =
                symbol is not null &&
                definitions.TryGetValue(symbol, out definitionSpan);
            IReadOnlyList<SemanticHoverItem> items = hoverItems ??
                (
                    hoverContent is null
                        ? []
                        :
                        [
                            new SemanticHoverItem(
                                hoverCategory ?? GetHoverCategory(kind),
                                hoverContent
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
                    isClassification
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
            string? hoverContent,
            SemanticClassificationModifiers modifiers
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
                hoverContent,
                modifiers | SemanticClassificationModifiers.Declaration
            );
        }

        string Source(TextSpan span)
        {
            return binding.Source.GetText(span);
        }

        string ExpressionDisplay(BoundExpression expression)
        {
            return SymbolDisplay.Expression(
                Source(expression.Span),
                expression.Type
            );
        }

        void AddOperator(
            SyntaxToken token,
            IReadOnlyList<TypeSymbol> operands,
            TypeSymbol result
        )
        {
            Add(
                null,
                SemanticClassificationKind.Operator,
                SemanticOccurrenceRole.Reference,
                token.Span,
                SymbolDisplay.Operator(
                    Source(token.Span),
                    operands,
                    result
                ),
                isClassification: false
            );
        }

        void VisitDynamicKey(
            BoundExpression key,
            ElementAccessExpressionSyntax syntax,
            TypeSymbol resultType
        )
        {
            string accessDisplay = SymbolDisplay.Expression(
                Source(syntax.Span),
                resultType
            );

            switch (key)
            {
                case BoundExpression.Local local:
                {
                    Add(
                        LocalKey(
                            local.Symbol,
                            SemanticClassificationKind.Variable
                        ),
                        SemanticClassificationKind.Variable,
                        SemanticOccurrenceRole.Read,
                        local.Span,
                        null,
                        local.Symbol.IsReadOnly
                            ? SemanticClassificationModifiers.ReadOnly
                            : SemanticClassificationModifiers.None,
                        hoverItems:
                        [
                            new SemanticHoverItem(
                                SemanticHoverCategory.Variable,
                                SymbolDisplay.Variable(local.Symbol)
                            ),
                            new SemanticHoverItem(
                                SemanticHoverCategory.Expression,
                                accessDisplay
                            ),
                        ]
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
                        SemanticOccurrenceRole.Read,
                        parameter.Span,
                        hoverItems:
                        [
                            new SemanticHoverItem(
                                SemanticHoverCategory.Parameter,
                                SymbolDisplay.Parameter(parameter.Symbol)
                            ),
                            new SemanticHoverItem(
                                SemanticHoverCategory.Expression,
                                accessDisplay
                            ),
                        ]
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
                        SemanticOccurrenceRole.Read,
                        global.Span,
                        null,
                        SemanticClassificationModifiers.ReadOnly |
                        SemanticClassificationModifiers.DefaultLibrary,
                        hoverItems:
                        [
                            new SemanticHoverItem(
                                SemanticHoverCategory.Variable,
                                SymbolDisplay.Variable(global.Symbol)
                            ),
                            new SemanticHoverItem(
                                SemanticHoverCategory.Expression,
                                accessDisplay
                            ),
                        ]
                    );
                    break;
                }

                case BoundExpression.Literal:
                {
                    Add(
                        null,
                        SemanticClassificationKind.Literal,
                        SemanticOccurrenceRole.Reference,
                        key.Span,
                        isClassification: false,
                        hoverItems:
                        [
                            new SemanticHoverItem(
                                SemanticHoverCategory.Literal,
                                ExpressionDisplay(key)
                            ),
                            new SemanticHoverItem(
                                SemanticHoverCategory.Expression,
                                accessDisplay
                            ),
                        ]
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: NameExpressionSyntax name,
                }:
                {
                    Add(
                        null,
                        SemanticClassificationKind.Variable,
                        SemanticOccurrenceRole.Read,
                        name.IdentifierToken.Span,
                        isClassification: false,
                        hoverItems:
                        [
                            new SemanticHoverItem(
                                SemanticHoverCategory.Variable,
                                SymbolDisplay.Unresolved(
                                    Source(name.IdentifierToken.Span)
                                )
                            ),
                            new SemanticHoverItem(
                                SemanticHoverCategory.Expression,
                                accessDisplay
                            ),
                        ]
                    );
                    break;
                }

                default:
                {
                    VisitExpression(
                        key,
                        SemanticOccurrenceRole.Read
                    );
                    Add(
                        null,
                        SemanticClassificationKind.Literal,
                        SemanticOccurrenceRole.Reference,
                        key.Span,
                        isClassification: false,
                        hoverItems:
                        [
                            new SemanticHoverItem(
                                SemanticHoverCategory.Expression,
                                ExpressionDisplay(key)
                            ),
                            new SemanticHoverItem(
                                SemanticHoverCategory.Expression,
                                accessDisplay
                            ),
                        ]
                    );
                    break;
                }
            }
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
                    SemanticClassificationKind kind =
                        SemanticClassificationKind.Type;
                    SemanticSymbolKey? symbol = type is null
                        ? null
                        : TypeKey(type);
                    Add(
                        symbol,
                        kind,
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
                    SemanticClassificationModifiers.None
                );
                VisitType(propertySyntax.Type);
            }
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
                            : SemanticClassificationModifiers.None
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
                    VisitExpression(
                        conditional.Condition,
                        SemanticOccurrenceRole.Read
                    );
                    VisitStatement(conditional.Then);

                    if (conditional.Else is not null)
                    {
                        VisitStatement(conditional.Else);
                    }

                    break;
                }

                case BoundStatement.While loop:
                {
                    VisitExpression(
                        loop.Condition,
                        SemanticOccurrenceRole.Read
                    );
                    VisitStatement(loop.Body);
                    break;
                }

                case BoundStatement.For loop:
                {
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

                    VisitStatement(loop.Body);
                    break;
                }

                case BoundStatement.Return { Value: not null } result:
                {
                    VisitExpression(
                        result.Value,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundStatement.Throw thrown:
                {
                    VisitExpression(
                        thrown.Error,
                        SemanticOccurrenceRole.Read
                    );
                    break;
                }

                case BoundStatement.Try guarded:
                {
                    VisitStatement(guarded.Protected);

                    if (guarded.ErrorLocal is not null)
                    {
                        SyntaxToken? identifier =
                            ((TryStatementSyntax)guarded.Syntax)
                                .CatchClause
                                ?.IdentifierToken;

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
                            Source(syntax.NameToken.Span)
                        ),
                        isClassification: false
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: ElementAccessExpressionSyntax syntax,
                    Children: [ BoundExpression target, BoundExpression key ],
                }:
                {
                    VisitExpression(
                        target,
                        SemanticOccurrenceRole.Read
                    );
                    VisitDynamicKey(
                        key,
                        syntax,
                        TypeSymbols.ErrorRecovery
                    );
                    break;
                }

                case BoundExpression.Error
                {
                    Syntax: UnaryExpressionSyntax syntax,
                    Children: [ BoundExpression operand ],
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
                        BoundExpression left,
                        BoundExpression right,
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
                        ObjectTypeSymbol? owner =
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
                        string? content = property is null
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
                                SemanticClassificationModifiers.None
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
                                SemanticClassificationModifiers.Declaration
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
                    string? content;

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
                            member.Type
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
                    else if (
                        element.IsObjectAccess &&
                        element.IsDynamic
                    )
                    {
                        VisitDynamicKey(
                            element.Index,
                            (ElementAccessExpressionSyntax)element.Syntax,
                            element.Type
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

                    foreach (BoundStatement statement in function.Statements)
                    {
                        VisitStatement(statement);
                    }
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
