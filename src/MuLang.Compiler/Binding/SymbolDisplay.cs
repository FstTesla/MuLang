using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal static class SymbolDisplay
{
    public static string Variable(LocalSymbol symbol)
    {
        return Variable(symbol.Name, symbol.Type, symbol.IsReadOnly);
    }

    public static string Variable(GlobalSymbol symbol)
    {
        return Variable(symbol.Name, symbol.Type, true);
    }

    public static string Parameter(UserParameterSymbol symbol)
    {
        return Parameter(symbol.Name, symbol.Type);
    }

    public static string Function(UserFunctionSymbol symbol)
    {
        return Function(
            symbol.Name,
            symbol.Parameters.Select(Parameter),
            symbol.ReturnType
        );
    }

    public static string Function(FunctionSymbol symbol)
    {
        return Function(
            symbol.Name,
            symbol.Parameters.Select(
                static parameter => Parameter(parameter.Name, parameter.Type)
            ),
            symbol.ReturnType
        );
    }

    public static string Variable(string name, TypeSymbol type, bool isReadOnly)
    {
        return $"{name}{(isReadOnly ? "$" : "")}: {Type(type)}";
    }

    public static string Parameter(string name, TypeSymbol type)
    {
        return $"{name}: {Type(type)}";
    }

    public static string Function(
        string name,
        IEnumerable<string> parameters,
        TypeSymbol returnType
    )
    {
        return $"{name}({string.Join(", ", parameters)}): {Type(returnType)}";
    }

    public static string Property(ObjectPropertySymbol property)
    {
        return $"{PropertyName(property.Name)}{(property.IsReadOnly ? "$" : "")}{(property.IsOptional ? "?" : "")}: {Type(property.Type)}";
    }

    public static string Unresolved(string name)
    {
        return $"{name}: {Type(TypeSymbols.ErrorRecovery)}";
    }

    public static string UnresolvedFunction(string name)
    {
        return $"{name}(...): {Type(TypeSymbols.ErrorRecovery)}";
    }

    public static string Expression(string source, TypeSymbol type)
    {
        return $"{source}: {Type(type)}";
    }

    public static string Operator(
        string symbol,
        IEnumerable<TypeSymbol> operands,
        TypeSymbol result
    )
    {
        return $"{symbol}({string.Join(", ", operands.Select(Type))}): {Type(result)}";
    }

    public static string Type(TypeSymbol type)
    {
        return type switch
        {
            ArrayTypeSymbol array =>
                $"{Type(array.ElementType)}[]{(array.IsReadOnly ? "$" : "")}",
            NullableTypeSymbol nullable => $"{Type(nullable.UnderlyingType)}?",
            ObjectTypeSymbol { Name: "<anonymous>" } value => ObjectBody(value),
            _ => type.DisplayName,
        };
    }

    public static string TypeDeclaration(TypeSymbol type)
    {
        return type is ObjectTypeSymbol value
            ? $"{value.Name} {ObjectBody(value)}"
            : type.DisplayName;
    }

    private static string ObjectBody(ObjectTypeSymbol type)
    {
        return $"{(type.IsOpen ? "@" : "")}{{ {string.Join(", ", type.Properties.Select(Property))} }}";
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
