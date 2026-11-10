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

        if (root is not ProgramRootSyntax program)
        {
            return builder;
        }

        IList<SemanticDocumentSymbol> symbols = new List<SemanticDocumentSymbol>();

        foreach (TypeDeclarationSyntax type in program.Types)
        {
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
                builder.AddSymbol(
                    children,
                    parameter.IdentifierToken,
                    SemanticDocumentSymbolKind.Parameter,
                    parameter.Span,
                    builder.TypeProperties(parameter.Type.Primary)
                );
            }

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
                if (
                    !block.OpenBraceToken.IsMissing &&
                    !block.CloseBraceToken.IsMissing &&
                    source.GetPosition(block.OpenBraceToken.Span.Start).Line <
                    source.GetPosition(block.CloseBraceToken.Span.Start).Line
                )
                {
                    foldingSpans.Add(block.Span);
                }

                foreach (StatementSyntax child in block.Statements)
                {
                    VisitStatement(child, symbols);
                }

                break;
            }

            case VariableDeclarationStatementSyntax variable:
            {
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
                VisitStatement(conditional.ThenStatement, symbols);

                if (conditional.ElseStatement is { } otherwise)
                {
                    VisitStatement(otherwise, symbols);
                }

                break;
            }

            case WhileStatementSyntax loop:
                VisitStatement(loop.Body, symbols);
                break;

            case ForStatementSyntax loop:
            {
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
