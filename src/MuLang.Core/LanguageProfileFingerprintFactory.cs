using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MuLang.Core;

internal static class LanguageProfileFingerprintFactory
{
    public static LanguageProfileFingerprint Create(LanguageProfile profile)
    {
        StringBuilder canonical = new ();
        Append(canonical, "LanguageVersion", profile.LanguageVersion);
        Append(canonical, "UserDefinedFunctions", profile.UserDefinedFunctions);
        Append(canonical, "Recursion", profile.Recursion);
        Append(canonical, "Loops", profile.Loops);
        Append(canonical, "ProviderFunctionCalls", profile.ProviderFunctionCalls);
        Append(canonical, "OpenObjects", profile.OpenObjects);
        Append(canonical, "Mutations", profile.Mutations);
        Append(canonical, "MultiLevelLoopControl", profile.MultiLevelLoopControl);
        Append(canonical, "TrailingCommas", profile.TrailingCommas);
        Append(canonical, "ConditionSemantics", profile.ConditionSemantics);
        Append(canonical, "Shadowing", profile.Shadowing);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));

        return new LanguageProfileFingerprint(Convert.ToHexStringLower(hash));
    }

    private static void Append<T>(StringBuilder canonical, string label, T value)
        where T : struct, Enum
    {
        AppendValue(canonical, label);
        AppendValue(
            canonical,
            Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture)
        );
    }

    private static void AppendValue(StringBuilder canonical, string value)
    {
        canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        canonical.Append(':');
        canonical.Append(value);
        canonical.Append(';');
    }
}
