using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed class DocumentStructureBuilder
{
    private readonly SourceText source;
    private readonly IList<TextSpan> foldingSpans = new List<TextSpan>();

    private DocumentStructureBuilder(SourceText source)
    {
        this.source = source;
    }

    public IReadOnlyList<SemanticDocumentSymbol> Symbols { get; private set; } = [ ];
    public IReadOnlyList<TextSpan> FoldingSpans { get; private set; } = [ ];

    public static DocumentStructureBuilder Build(SyntaxNode root, SourceText source)
    {
        DocumentStructureBuilder builder = new (source);

        IList<SemanticDocumentSymbol> symbols = new List<SemanticDocumentSymbol>();

        if (root is ExpressionRootSyntax expression)
        {
            builder.VisitExpression(expression.Expression);
        }
        else if (root is ProgramRootSyntax program)
        {
            foreach (TypeDeclarationSyntax type in program.Types)
            {
                builder.VisitType(type.Body);
                builder.AddSymbol(
                    symbols,
                    type.IdentifierToken,
                    SemanticDocumentSymbolKind.Type,
                    type.Span,
                    builder.TypeProperties(type.Body)
                );
            }

            foreach (FunctionDeclarationSyntax function in program.Functions)
            {
                IList<SemanticDocumentSymbol> children = new List<SemanticDocumentSymbol>();

                foreach (ParameterSyntax parameter in function.Parameters)
                {
                    builder.VisitType(parameter.Type.Primary);
                    builder.AddSymbol(
                        children,
                        parameter.IdentifierToken,
                        SemanticDocumentSymbolKind.Parameter,
                        parameter.Span,
                        builder.TypeProperties(parameter.Type.Primary)
                    );
                }

                builder.VisitType(function.ReturnType.Primary);
                builder.VisitStatement(function.Body, children);
                builder.AddSymbol(
                    symbols,
                    function.IdentifierToken,
                    SemanticDocumentSymbolKind.Function,
                    function.Span,
                    children
                );
            }

            foreach (StatementSyntax statement in program.Statements)
            {
                builder.VisitStatement(statement, symbols);
            }
        }

        builder.Symbols = [ .. symbols.OrderBy(static symbol => symbol.Span.Start) ];
        builder.FoldingSpans = [ .. builder.foldingSpans.Distinct().OrderBy(static span => span.Start) ];
        return builder;
    }

    private void VisitStatement(StatementSyntax statement, IList<SemanticDocumentSymbol> symbols)
    {
        switch (statement)
        {
            case BlockStatementSyntax block:
            {
                AddFold(block.OpenBraceToken, block.CloseBraceToken);

                foreach (StatementSyntax child in block.Statements)
                {
                    VisitStatement(child, symbols);
                }

                break;
            }

            case VariableDeclarationStatementSyntax variable:
            {
                VisitType(variable.Type?.Primary);
                VisitExpression(variable.Initializer);
                IReadOnlyList<SemanticDocumentSymbol> children = variable.Type?.Primary is ObjectTypeBodySyntax body
                    ? TypeProperties(body)
                    : ValueProperties(variable.Initializer);
                AddSymbol(
                    symbols,
                    variable.IdentifierToken,
                    SemanticDocumentSymbolKind.Variable,
                    variable.Span,
                    children
                );
                break;
            }

            case IfStatementSyntax conditional:
            {
                VisitExpression(conditional.Condition);
                VisitStatement(conditional.ThenStatement, symbols);

                if (conditional.ElseStatement is { } otherwise)
                {
                    VisitStatement(otherwise, symbols);
                }

                break;
            }

            case WhileStatementSyntax loop:
                VisitExpression(loop.Condition);
                VisitStatement(loop.Body, symbols);
                break;

            case ForStatementSyntax loop:
            {
                VisitExpression(loop.Condition);
                if (loop.Initializer is { } initializer)
                {
                    VisitStatement(initializer, symbols);
                }

                if (loop.Iterator is { } iterator)
                {
                    VisitStatement(iterator, symbols);
                }

                VisitStatement(loop.Body, symbols);
                break;
            }

            case TryStatementSyntax protectedStatement:
            {
                VisitStatement(protectedStatement.Body, symbols);

                if (protectedStatement.CatchClause is { } handler)
                {
                    if (handler.IdentifierToken is { } identifier)
                    {
                        AddSymbol(
                            symbols,
                            identifier,
                            SemanticDocumentSymbolKind.Variable,
                            identifier.Span,
                            [ ]
                        );
                    }

                    VisitStatement(handler.Body, symbols);
                }

                if (protectedStatement.FinallyClause is { } cleanup)
                {
                    VisitStatement(cleanup.Body, symbols);
                }

                break;
            }

            case ExpressionStatementSyntax expression:
                VisitExpression(expression.Expression);
                break;

            case AssignmentStatementSyntax assignment:
                VisitExpression(assignment.Target);
                VisitExpression(assignment.Value);
                break;

            case RemovalStatementSyntax removal:
                VisitExpression(removal.Target);
                break;

            case ReturnStatementSyntax result:
                VisitExpression(result.Expression);
                break;

            case ThrowStatementSyntax thrown:
                VisitExpression(thrown.Expression);
                break;
        }
    }

    private void VisitType(TypePrimarySyntax? primary)
    {
        if (primary is not ObjectTypeBodySyntax body)
        {
            return;
        }

        AddFold(body.OpenBraceToken, body.CloseBraceToken);

        foreach (ObjectTypePropertySyntax property in body.Properties)
        {
            VisitType(property.Type.Primary);
        }
    }

    private void VisitExpression(ExpressionSyntax? expression)
    {
        switch (expression)
        {
            case ObjectLiteralExpressionSyntax body:
            {
                AddFold(body.OpenBraceToken, body.CloseBraceToken);

                foreach (ObjectPropertyInitializerSyntax property in body.Properties)
                {
                    VisitType(property.Type?.Primary);
                    VisitExpression(property.Value);
                }

                break;
            }

            case ArrayLiteralExpressionSyntax array:
            {
                AddFold(array.OpenBracketToken, array.CloseBracketToken);

                foreach (ExpressionSyntax element in array.Elements)
                {
                    VisitExpression(element);
                }

                break;
            }

            case ParenthesizedExpressionSyntax parenthesized:
                VisitExpression(parenthesized.Expression);
                break;

            case UnaryExpressionSyntax unary:
                VisitExpression(unary.Operand);
                break;

            case BinaryExpressionSyntax binary:
                VisitExpression(binary.Left);
                VisitExpression(binary.Right);
                break;

            case ConditionalExpressionSyntax conditional:
                VisitExpression(conditional.Condition);
                VisitExpression(conditional.WhenTrue);
                VisitExpression(conditional.WhenFalse);
                break;

            case CallExpressionSyntax call:
            {
                VisitExpression(call.Target);

                foreach (ExpressionSyntax argument in call.Arguments)
                {
                    VisitExpression(argument);
                }

                break;
            }

            case MemberAccessExpressionSyntax member:
                VisitExpression(member.Target);
                break;

            case ElementAccessExpressionSyntax element:
                VisitExpression(element.Target);
                VisitExpression(element.Index);
                break;

            case PropertyTestExpressionSyntax property:
                VisitExpression(property.Target);
                VisitExpression(property.Key);
                break;

            case ConversionExpressionSyntax conversion:
                VisitExpression(conversion.Expression);
                VisitType(conversion.Type.Primary);
                break;

            case TypeTestExpressionSyntax test:
                VisitExpression(test.Expression);
                VisitType(test.Type.Primary);
                break;
        }
    }

    private void AddFold(SyntaxToken open, SyntaxToken close)
    {
        if (
            !open.IsMissing &&
            !close.IsMissing &&
            source.GetPosition(open.Span.Start).Line < source.GetPosition(close.Span.Start).Line
        )
        {
            foldingSpans.Add(TextSpan.FromBounds(open.Span.Start, close.Span.End));
        }
    }

    private IReadOnlyList<SemanticDocumentSymbol> TypeProperties(TypePrimarySyntax? primary)
    {
        if (primary is not ObjectTypeBodySyntax body)
        {
            return [ ];
        }

        IList<SemanticDocumentSymbol> symbols = new List<SemanticDocumentSymbol>();

        foreach (ObjectTypePropertySyntax property in body.Properties)
        {
            AddSymbol(
                symbols,
                property.NameToken,
                SemanticDocumentSymbolKind.Property,
                property.Span,
                TypeProperties(property.Type.Primary)
            );
        }

        return [ .. symbols ];
    }

    private IReadOnlyList<SemanticDocumentSymbol> ValueProperties(ExpressionSyntax? expression)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            return ValueProperties(parenthesized.Expression);
        }

        if (expression is not ObjectLiteralExpressionSyntax body)
        {
            return [ ];
        }

        IList<SemanticDocumentSymbol> symbols = new List<SemanticDocumentSymbol>();

        foreach (ObjectPropertyInitializerSyntax property in body.Properties)
        {
            IReadOnlyList<SemanticDocumentSymbol> children = property.Type?.Primary is ObjectTypeBodySyntax type
                ? TypeProperties(type)
                : ValueProperties(property.Value);
            AddSymbol(
                symbols,
                property.NameToken,
                SemanticDocumentSymbolKind.Property,
                property.Span,
                children
            );
        }

        return [ .. symbols ];
    }

    private void AddSymbol(
        IList<SemanticDocumentSymbol> symbols,
        SyntaxToken identifier,
        SemanticDocumentSymbolKind kind,
        TextSpan span,
        IEnumerable<SemanticDocumentSymbol> children
    )
    {
        if (identifier.IsMissing || identifier.Span.Length == 0)
        {
            return;
        }

        symbols.Add(
            new SemanticDocumentSymbol(
                identifier.Value ?? source.GetText(identifier.Span),
                kind,
                span,
                identifier.Span,
                [ .. children.OrderBy(static child => child.Span.Start) ]
            )
        );
    }
}
