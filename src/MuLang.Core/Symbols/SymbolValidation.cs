using MuLang.Core.Types;

namespace MuLang.Core.Symbols;

internal static class SymbolValidation
{
    public static void ValidateId(string id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Symbol identifier cannot be null or whitespace.",
                parameterName
            );
        }
    }

    public static void ValidateValueType(TypeSymbol type, string parameterName)
    {
        if (type is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (type.Kind is TypeKind.Void or TypeKind.Null or TypeKind.Error)
        {
            throw new ArgumentException(
                $"Type '{type.DisplayName}' cannot be used as a value type.",
                parameterName
            );
        }
    }
}
