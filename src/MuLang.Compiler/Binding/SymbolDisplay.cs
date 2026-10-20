using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal static class SymbolDisplay
{
    public static IReadOnlyList<SemanticHoverPart> Variable(LocalSymbol symbol)
    {
        return Variable(symbol.Name, symbol.Type, symbol.IsReadOnly);
    }

    public static IReadOnlyList<SemanticHoverPart> Variable(GlobalSymbol symbol)
    {
        return Variable(symbol.Name, symbol.Type, true);
    }

    public static IReadOnlyList<SemanticHoverPart> Parameter(UserParameterSymbol symbol)
    {
        return Parameter(symbol.Name, symbol.Type);
    }

    public static IReadOnlyList<SemanticHoverPart> Function(UserFunctionSymbol symbol)
    {
        return Function(
            symbol.Name,
            [ .. symbol.Parameters.Select(static parameter => (parameter.Name, parameter.Type)) ],
            symbol.ReturnType
        );
    }

    public static IReadOnlyList<SemanticHoverPart> Function(FunctionSymbol symbol)
    {
        return Function(
            symbol.Name,
            [ .. symbol.Parameters.Select(static parameter => (parameter.Name, parameter.Type)) ],
            symbol.ReturnType
        );
    }

    public static IReadOnlyList<SemanticHoverPart> Variable(
        string name,
        TypeSymbol type,
        bool isReadOnly
    )
    {
        Builder builder = new ();
        builder.Add(SemanticHoverPartKind.Variable, name);

        if (isReadOnly)
        {
            builder.Add(SemanticHoverPartKind.Operator, "$");
        }

        builder.AppendResult(type, 0);
        return builder.Build();
    }

    public static IReadOnlyList<SemanticHoverPart> Parameter(string name, TypeSymbol type)
    {
        Builder builder = new ();
        builder.AppendParameter((name, type), 0);
        return builder.Build();
    }

    public static IReadOnlyList<SemanticHoverPart> Function(
        string name,
        IReadOnlyList<(string Name, TypeSymbol Type)> parameters,
        TypeSymbol returnType
    )
    {
        Builder builder = new ();
        builder.Add(SemanticHoverPartKind.Function, name);
        builder.AppendList("(", ")", parameters, 0, builder.AppendParameter);
        builder.AppendResult(returnType, 0);
        return builder.Build();
    }

    public static IReadOnlyList<SemanticHoverPart> Property(ObjectPropertySymbol property)
    {
        Builder builder = new ();
        builder.AppendProperty(property, 0);
        return builder.Build();
    }

    public static IReadOnlyList<SemanticHoverPart> Unresolved(
        string name,
        SemanticHoverPartKind kind = SemanticHoverPartKind.Variable
    )
    {
        return Expression(name, TypeSymbols.ErrorRecovery, kind);
    }

    public static IReadOnlyList<SemanticHoverPart> UnresolvedFunction(string name)
    {
        Builder builder = new ();
        builder.Add(SemanticHoverPartKind.Function, name);
        builder.Add(SemanticHoverPartKind.Punctuation, "(...)");
        builder.AppendResult(TypeSymbols.ErrorRecovery, 0);
        return builder.Build();
    }

    public static IReadOnlyList<SemanticHoverPart> Expression(
        string source,
        TypeSymbol type,
        SemanticHoverPartKind kind
    )
    {
        Builder builder = new ();
        builder.Add(kind, source);
        builder.AppendResult(type, 0);
        return builder.Build();
    }

    public static IReadOnlyList<SemanticHoverPart> Operator(
        string symbol,
        IEnumerable<TypeSymbol> operands,
        TypeSymbol result
    )
    {
        Builder builder = new ();
        builder.Add(SemanticHoverPartKind.Operator, symbol);
        builder.Add(SemanticHoverPartKind.Punctuation, "(");
        bool first = true;

        foreach (TypeSymbol operand in operands)
        {
            if (!first)
            {
                builder.Add(SemanticHoverPartKind.Punctuation, ",");
                builder.Add(SemanticHoverPartKind.Text, " ");
            }

            builder.AppendType(operand, 0);
            first = false;
        }

        builder.Add(SemanticHoverPartKind.Punctuation, ")");
        builder.AppendResult(result, 0);
        return builder.Build();
    }

    public static IReadOnlyList<SemanticHoverPart> TypeDeclaration(TypeSymbol type)
    {
        Builder builder = new ();

        if (type is ObjectTypeSymbol value)
        {
            builder.Add(SemanticHoverPartKind.Type, value.Name);
            builder.Add(SemanticHoverPartKind.Text, " ");
            builder.AppendObjectBody(value, 0);
        }
        else
        {
            builder.AppendType(type, 0);
        }

        return builder.Build();
    }

    private sealed class Builder
    {
        private readonly IList<SemanticHoverPart> parts = new List<SemanticHoverPart>();

        public IReadOnlyList<SemanticHoverPart> Build()
        {
            return [ .. parts ];
        }

        public void Add(SemanticHoverPartKind kind, string text)
        {
            parts.Add(new SemanticHoverPart(kind, text));
        }

        public void AppendResult(TypeSymbol type, int indentation)
        {
            Add(SemanticHoverPartKind.Punctuation, ":");
            Add(SemanticHoverPartKind.Text, " ");
            AppendType(type, indentation);
        }

        public void AppendParameter(
            (string Name, TypeSymbol Type) parameter,
            int indentation
        )
        {
            Add(SemanticHoverPartKind.Parameter, parameter.Name);
            AppendResult(parameter.Type, indentation);
        }

        public void AppendProperty(ObjectPropertySymbol property, int indentation)
        {
            Add(SemanticHoverPartKind.Property, PropertyName(property.Name));

            if (property.IsReadOnly)
            {
                Add(SemanticHoverPartKind.Operator, "$");
            }

            if (property.IsOptional)
            {
                Add(SemanticHoverPartKind.Operator, "?");
            }

            AppendResult(property.Type, indentation);
        }

        public void AppendType(TypeSymbol type, int indentation)
        {
            switch (type)
            {
                case ArrayTypeSymbol array:
                {
                    AppendType(array.ElementType, indentation);
                    Add(SemanticHoverPartKind.Punctuation, "[]");

                    if (array.IsReadOnly)
                    {
                        Add(SemanticHoverPartKind.Operator, "$");
                    }

                    break;
                }

                case NullableTypeSymbol nullable:
                {
                    AppendType(nullable.UnderlyingType, indentation);
                    Add(SemanticHoverPartKind.Operator, "?");
                    break;
                }

                case ObjectTypeSymbol { Name: "<anonymous>" } value:
                {
                    AppendObjectBody(value, indentation);
                    break;
                }

                default:
                {
                    Add(
                        type.Kind switch
                        {
                            TypeKind.StructuredObject => SemanticHoverPartKind.Type,
                            TypeKind.ErrorRecovery or TypeKind.Null => SemanticHoverPartKind.Text,
                            _ => SemanticHoverPartKind.Keyword,
                        },
                        type.DisplayName
                    );
                    break;
                }
            }
        }

        public void AppendObjectBody(ObjectTypeSymbol type, int indentation)
        {
            if (type.IsOpen)
            {
                Add(SemanticHoverPartKind.Operator, "@");
            }

            AppendList(
                "{",
                "}",
                type.Properties,
                indentation,
                AppendProperty,
                true
            );
        }

        public void AppendList<T>(
            string open,
            string close,
            IReadOnlyCollection<T> values,
            int indentation,
            Action<T, int> append,
            bool spacesInside = false
        )
        {
            bool multiline = values.Count >= 2;
            Add(SemanticHoverPartKind.Punctuation, open);

            bool first = true;

            foreach (T value in values)
            {
                if (!first)
                {
                    Add(SemanticHoverPartKind.Punctuation, ",");
                }

                if (multiline)
                {
                    Add(
                        SemanticHoverPartKind.Text,
                        $"\n{new string(' ', (indentation + 1) * 2)}"
                    );
                }
                else if (spacesInside)
                {
                    Add(SemanticHoverPartKind.Text, " ");
                }

                append(value, multiline ? indentation + 1 : indentation);
                first = false;
            }

            if (multiline)
            {
                Add(SemanticHoverPartKind.Text, $"\n{new string(' ', indentation * 2)}");
            }
            else if (spacesInside && values.Count > 0)
            {
                Add(SemanticHoverPartKind.Text, " ");
            }

            Add(SemanticHoverPartKind.Punctuation, close);
        }
    }

    private static string PropertyName(string name)
    {
        SourceText source = SourceText.From(name);
        LexResult lexing = Lexer.Lex(source, LanguageProfiles.Latest);

        if (
            lexing.Tokens is [ { Kind: TokenKind.Identifier } identifier, { Kind: TokenKind.EndOfFile } ] &&
            identifier.Span.Length == source.Length
        )
        {
            return name;
        }

        string escaped = name
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\0", "\\0", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
