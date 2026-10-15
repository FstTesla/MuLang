using System.Text.Json;

namespace MuLang.LanguageServer;

internal static class WorkspaceConfigurationSetParser
{
    public static WorkspaceConfigurationSet Parse(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return WorkspaceConfigurationSet.Default;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("The configuration must be an object.");
        }

        ValidateProperties(element, "configuration", "overrides");

        WorkspaceConfiguration configuration = element.TryGetProperty(
            "configuration",
            out JsonElement configurationElement
        )
            ? WorkspaceConfigurationParser.Parse(configurationElement)
            : WorkspaceConfiguration.Default;
        IReadOnlyList<WorkspaceConfigurationOverride> overrides =
            element.TryGetProperty(
                "overrides",
                out JsonElement overridesElement
            )
            ? ParseOverrides(overridesElement)
            : [];

        return new WorkspaceConfigurationSet(configuration, overrides);
    }

    private static IReadOnlyList<WorkspaceConfigurationOverride> ParseOverrides(
        JsonElement element
    )
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("'overrides' must be an array.");
        }

        IList<WorkspaceConfigurationOverride> overrides =
            new List<WorkspaceConfigurationOverride>();
        int index = 0;

        foreach (JsonElement configurationOverride in element.EnumerateArray())
        {
            string path = $"overrides[{index}]";

            if (configurationOverride.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"'{path}' must be an object.");
            }

            ValidateProperties(
                configurationOverride,
                "includes",
                "excludes",
                "configuration"
            );

            if (
                !configurationOverride.TryGetProperty(
                    "includes",
                    out JsonElement includesElement
                )
            )
            {
                throw new FormatException($"'{path}.includes' is required.");
            }

            if (
                !configurationOverride.TryGetProperty(
                    "configuration",
                    out JsonElement overrideConfigurationElement
                )
            )
            {
                throw new FormatException($"'{path}.configuration' is required.");
            }

            IReadOnlyList<WorkspacePathGlob> includes = ParseGlobs(
                includesElement,
                $"{path}.includes",
                false
            );
            IReadOnlyList<WorkspacePathGlob> excludes =
                configurationOverride.TryGetProperty(
                    "excludes",
                    out JsonElement excludesElement
                )
                    ? ParseGlobs(excludesElement, $"{path}.excludes", true)
                    : [];
            WorkspaceConfiguration overrideConfiguration =
                WorkspaceConfigurationParser.Parse(
                    overrideConfigurationElement
                );
            overrides.Add(
                new WorkspaceConfigurationOverride(
                    includes,
                    excludes,
                    overrideConfiguration
                )
            );
            index++;
        }

        return [ .. overrides ];
    }

    private static IReadOnlyList<WorkspacePathGlob> ParseGlobs(
        JsonElement element,
        string path,
        bool allowEmpty
    )
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"'{path}' must be an array of strings.");
        }

        IList<WorkspacePathGlob> globs = new List<WorkspacePathGlob>();
        int index = 0;

        foreach (JsonElement item in element.EnumerateArray())
        {
            globs.Add(
                new WorkspacePathGlob(
                    ReadString(item, $"{path}[{index}]")
                )
            );
            index++;
        }

        if (!allowEmpty && globs.Count == 0)
        {
            throw new FormatException($"'{path}' must not be empty.");
        }

        return [ .. globs ];
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"'{path}' must be a string.");
        }

        string? value = element.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"'{path}' must not be empty.");
        }

        return value;
    }

    private static void ValidateProperties(
        JsonElement element,
        params string[] allowedProperties
    )
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new FormatException(
                    $"Unknown configuration property '{property.Name}'."
                );
            }
        }
    }
}
