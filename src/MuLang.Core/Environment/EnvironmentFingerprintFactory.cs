using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MuLang.Core.Environment;

internal static class EnvironmentFingerprintFactory
{
    public static EnvironmentFingerprint Create(
        LanguageVersion languageVersion,
        IReadOnlyCollection<ObjectTypeSymbol> types,
        IReadOnlyCollection<GlobalSymbol> globals,
        IReadOnlyCollection<FunctionSymbol> functions
    )
    {
        StringBuilder canonical = new ();
        AppendValue(canonical, ((int)languageVersion).ToString(CultureInfo.InvariantCulture));

        foreach (ObjectTypeSymbol type in types.OrderBy(
                static type => type.Id,
                StringComparer.Ordinal
            ))
        {
            AppendValue(canonical, "type");
            AppendValue(canonical, GetProviderTypeId(type));
            AppendValue(canonical, type.Name);
            AppendValue(canonical, type.IsOpen ? "open" : "closed");

            foreach (ObjectPropertySymbol property in type.Properties.OrderBy(
                    static property => property.Name,
                    StringComparer.Ordinal
                ))
            {
                AppendValue(canonical, property.Name);
                AppendValue(canonical, property.IsOptional ? "optional" : "required");
                AppendType(canonical, property.Type);
            }
        }

        foreach (GlobalSymbol global in globals.OrderBy(
                static global => global.Id,
                StringComparer.Ordinal
            ))
        {
            AppendValue(canonical, "global");
            AppendValue(canonical, global.Id);
            AppendValue(canonical, global.Name);
            AppendType(canonical, global.Type);
        }

        foreach (FunctionSymbol function in functions.OrderBy(
                static function => function.Id,
                StringComparer.Ordinal
            ))
        {
            AppendValue(canonical, "function");
            AppendValue(canonical, function.Id);
            AppendValue(canonical, function.Name);

            foreach (ParameterSymbol parameter in function.Parameters)
            {
                AppendValue(canonical, parameter.Name);
                AppendType(canonical, parameter.Type);
            }

            AppendType(canonical, function.ReturnType);
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));

        return new EnvironmentFingerprint(Convert.ToHexStringLower(hash));
    }

    private static void AppendType(StringBuilder canonical, TypeSymbol type)
    {
        AppendValue(canonical, ((int)type.Kind).ToString(CultureInfo.InvariantCulture));

        switch (type)
        {
            case NullableTypeSymbol nullable:
            {
                AppendType(canonical, nullable.UnderlyingType);
                break;
            }

            case ArrayTypeSymbol array:
            {
                AppendType(canonical, array.ElementType);
                break;
            }

            case ObjectTypeSymbol structuredObject:
            {
                AppendValue(canonical, GetProviderTypeId(structuredObject));
                break;
            }
        }
    }

    private static void AppendValue(StringBuilder canonical, string value)
    {
        canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        canonical.Append(':');
        canonical.Append(value);
        canonical.Append(';');
    }

    private static string GetProviderTypeId(ObjectTypeSymbol type)
    {
        return type.Id ??
            throw new InvalidOperationException(
                "Anonymous structured types cannot contribute to an environment fingerprint."
            );
    }
}
