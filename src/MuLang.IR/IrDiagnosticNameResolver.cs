using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Provides diagnostic display-name resolution for well-known IR attributes.</summary>
public static class IrDiagnosticNameResolver
{
    /// <summary>Gets a function display name.</summary>
    /// <param name="attributes">The program attributes.</param>
    /// <param name="functionId">The portable function identifier.</param>
    /// <param name="fallback">The display name used when the function target is absent.</param>
    /// <returns>The diagnostic display name.</returns>
    public static string GetFunctionDisplayName(
        IrAttributeCollection attributes,
        string functionId,
        string fallback
    )
    {
        return attributes.TryGetFunction(functionId, out IrFunctionAttributes? value)
            ? value.QualifiedSourceName ??
            value.SourceName ??
            $"<{functionId}>"
            : fallback;
    }

    /// <summary>Gets a slot display name.</summary>
    /// <param name="attributes">The program attributes.</param>
    /// <param name="functionId">The containing function identifier.</param>
    /// <param name="slotId">The function-local slot identifier.</param>
    /// <param name="fallback">The display name used when the slot target is absent.</param>
    /// <returns>The diagnostic display name.</returns>
    public static string GetSlotDisplayName(
        IrAttributeCollection attributes,
        string functionId,
        int slotId,
        string fallback
    )
    {
        return attributes.TryGetSlot(functionId, slotId, out IrSlotAttributes? value)
            ? value.SourceName ?? $"%{slotId}"
            : fallback;
    }

    /// <summary>Gets the diagnostic aliases for a type.</summary>
    /// <param name="attributes">The program attributes.</param>
    /// <param name="type">The projected type.</param>
    /// <param name="fallback">The display name used when the type target is absent.</param>
    /// <returns>The ordered diagnostic aliases.</returns>
    public static IReadOnlyList<string> GetTypeDisplayNames(
        IrAttributeCollection attributes,
        TypeSymbol type,
        string fallback
    )
    {
        if (!attributes.TryGetType(type, out IrTypeAttributes? value))
        {
            return [ fallback ];
        }

        return value.SourceNames.Count > 0
            ? value.SourceNames
            : [ $"<{type.DisplayName}>" ];
    }

    /// <summary>Gets a provider-global display name.</summary>
    /// <param name="attributes">The program attributes.</param>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="fallback">The display name used when the provider-global mapping is absent.</param>
    /// <returns>The diagnostic display name.</returns>
    public static string GetProviderGlobalDisplayName(
        IrAttributeCollection attributes,
        string providerId,
        string fallback
    )
    {
        return GetProviderDisplayName(
            attributes.Program?.ProviderGlobalNames,
            providerId,
            fallback
        );
    }

    /// <summary>Gets a provider-function display name.</summary>
    /// <param name="attributes">The program attributes.</param>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="fallback">The display name used when the provider-function mapping is absent.</param>
    /// <returns>The diagnostic display name.</returns>
    public static string GetProviderFunctionDisplayName(
        IrAttributeCollection attributes,
        string providerId,
        string fallback
    )
    {
        return GetProviderDisplayName(
            attributes.Program?.ProviderFunctionNames,
            providerId,
            fallback
        );
    }

    private static string GetProviderDisplayName(
        IReadOnlyList<IrProviderSymbolName>? values,
        string providerId,
        string fallback
    )
    {
        IrProviderSymbolName? value = values?.FirstOrDefault(
            value => value.Id == providerId
        );

        if (value is null)
        {
            return fallback;
        }

        return string.IsNullOrWhiteSpace(value.Name)
            ? $"<{providerId}>"
            : value.Name;
    }
}
