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
            throw new WorkspaceConfigurationException(
                "The configuration must be an object.",
                element
            );
        }

        ValidateProperties(element, "$schema", "configuration", "overrides");

        if (
            element.TryGetProperty("$schema", out JsonElement schemaElement) &&
            schemaElement.ValueKind != JsonValueKind.String
        )
        {
            throw new WorkspaceConfigurationException(
                "'$schema' must be a string.",
                schemaElement
            );
        }

        WorkspaceConfiguration? configuration = element.TryGetProperty(
            "configuration",
            out JsonElement configurationElement
        )
            ? ParseConfiguration(configurationElement)
            : null;
        IReadOnlyList<WorkspaceConfigurationOverride> overrides =
            element.TryGetProperty(
                "overrides",
                out JsonElement overridesElement
            )
                ? ParseOverrides(overridesElement)
                : [ ];

        return new WorkspaceConfigurationSet(configuration, overrides);
    }

    private static IReadOnlyList<WorkspaceConfigurationOverride> ParseOverrides(
        JsonElement element
    )
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new WorkspaceConfigurationException(
                "'overrides' must be an array.",
                element
            );
        }

        IList<WorkspaceConfigurationOverride> overrides =
            new List<WorkspaceConfigurationOverride>();
        int index = 0;

        foreach (JsonElement configurationOverride in element.EnumerateArray())
        {
            string path = $"overrides[{index}]";

            if (configurationOverride.ValueKind != JsonValueKind.Object)
            {
                throw new WorkspaceConfigurationException(
                    $"'{path}' must be an object.",
                    configurationOverride
                );
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
                throw new WorkspaceConfigurationException(
                    $"'{path}.includes' is required.",
                    configurationOverride
                );
            }

            if (
                !configurationOverride.TryGetProperty(
                    "configuration",
                    out JsonElement overrideConfigurationElement
                )
            )
            {
                throw new WorkspaceConfigurationException(
                    $"'{path}.configuration' is required.",
                    configurationOverride
                );
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
                    : [ ];
            WorkspaceConfiguration overrideConfiguration =
                ParseConfiguration(
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
            throw new WorkspaceConfigurationException(
                $"'{path}' must be an array of strings.",
                element
            );
        }

        IList<WorkspacePathGlob> globs = new List<WorkspacePathGlob>();
        int index = 0;

        foreach (JsonElement item in element.EnumerateArray())
        {
            string glob = ReadString(item, $"{path}[{index}]");

            try
            {
                globs.Add(new WorkspacePathGlob(glob));
            }
            catch (FormatException exception)
            {
                throw new WorkspaceConfigurationException(
                    exception.Message,
                    item
                );
            }

            index++;
        }

        if (!allowEmpty && globs.Count == 0)
        {
            throw new WorkspaceConfigurationException(
                $"'{path}' must not be empty.",
                element
            );
        }

        return [ .. globs ];
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new WorkspaceConfigurationException(
                $"'{path}' must be a string.",
                element
            );
        }

        string? value = element.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new WorkspaceConfigurationException(
                $"'{path}' must not be empty.",
                element
            );
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
                throw new WorkspaceConfigurationException(
                    $"Unknown configuration property '{property.Name}'.",
                    property.Value
                );
            }
        }
    }

    private static WorkspaceConfiguration ParseConfiguration(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new WorkspaceConfigurationException(
                "'configuration' must be an object.",
                element
            );
        }

        return WorkspaceConfigurationParser.Parse(element);
    }
}
