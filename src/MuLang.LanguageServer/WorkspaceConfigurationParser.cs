using MuLang.Compiler;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Text.Json;

namespace MuLang.LanguageServer;

internal static class WorkspaceConfigurationParser
{
    public static WorkspaceConfiguration Parse(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return WorkspaceConfiguration.Default;
        }

        try
        {
            EnsureObject(element, "configuration");
            ValidateProperties(
                element,
                "configuration",
                "compilationMode",
                "profile",
                "environment",
                "expectedResultType"
            );

            CompilationMode compilationMode = element.TryGetProperty(
                "compilationMode",
                out JsonElement compilationModeElement
            )
                ? ParseEnum<CompilationMode>(compilationModeElement, "compilationMode")
                : CompilationMode.Program;
            LanguageProfile profile = element.TryGetProperty(
                "profile",
                out JsonElement profileElement
            )
                ? ParseProfile(profileElement)
                : LanguageProfiles.Latest;

            ObjectTypeGraphBuilder graph = new ();
            Dictionary<string, ObjectTypeGraphReference> namedTypes =
                new Dictionary<string, ObjectTypeGraphReference>(StringComparer.Ordinal);
            IList<ObjectTypeGraphReference> declaredTypes = new List<ObjectTypeGraphReference>();
            IList<GlobalDeclaration> globals = new List<GlobalDeclaration>();
            IList<FunctionDeclaration> functions = new List<FunctionDeclaration>();

            if (
                element.TryGetProperty("environment", out JsonElement environmentElement) &&
                environmentElement.ValueKind != JsonValueKind.Null
            )
            {
                ParseEnvironmentDeclarations(
                    environmentElement,
                    graph,
                    namedTypes,
                    declaredTypes,
                    globals,
                    functions
                );
            }

            ObjectTypeGraphReference? expectedResultType = null;

            if (
                element.TryGetProperty(
                    "expectedResultType",
                    out JsonElement expectedResultTypeElement
                ) &&
                expectedResultTypeElement.ValueKind != JsonValueKind.Null
            )
            {
                expectedResultType = ParseType(
                    expectedResultTypeElement,
                    "expectedResultType",
                    graph,
                    namedTypes
                );
            }

            IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> types = graph.Build();
            EnvironmentBuilder environmentBuilder = new ();

            foreach (ObjectTypeGraphReference typeReference in declaredTypes)
            {
                environmentBuilder.AddType((ObjectTypeSymbol)types[typeReference]);
            }

            foreach (GlobalDeclaration global in globals)
            {
                environmentBuilder.AddGlobal(
                    global.Id,
                    global.Name,
                    types[global.Type]
                );
            }

            foreach (FunctionDeclaration function in functions)
            {
                environmentBuilder.AddFunction(
                    function.Id,
                    function.Name,
                    function.Parameters.Select(
                        parameter => new ParameterSymbol(
                            parameter.Name,
                            types[parameter.Type]
                        )
                    ),
                    types[function.ReturnType]
                );
            }

            EnvironmentSchema environment = environmentBuilder.Build(
                profile.LanguageVersion
            );
            TypeSymbol? resolvedExpectedResultType = expectedResultType is null
                ? null
                : types[expectedResultType];
            _ = MuLangCompiler.Analyze(
                string.Empty,
                environment,
                compilationMode,
                new MuLangCompilationOptions
                {
                    ExpectedResultType = resolvedExpectedResultType,
                    Profile = profile,
                }
            );

            return new WorkspaceConfiguration(
                compilationMode,
                profile,
                environment,
                resolvedExpectedResultType
            );
        }
        catch (FormatException)
        {
            throw;
        }
        catch (
            Exception exception
        ) when (
            exception is ArgumentException or InvalidOperationException or KeyNotFoundException
        )
        {
            throw new FormatException(exception.Message, exception);
        }
    }

    private static LanguageProfile ParseProfile(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return ParseBaseProfile(ReadString(element, "profile"));
        }

        EnsureObject(element, "profile");
        ValidateProperties(
            element,
            "profile",
            "base",
            "languageVersion",
            "userDefinedFunctions",
            "userDefinedTypes",
            "recursion",
            "loops",
            "providerFunctionCalls",
            "openObjects",
            "mutations",
            "multiLevelLoopControl",
            "trailingCommas",
            "constantFolding",
            "exceptionHandling",
            "objectLiteralSyntax",
            "conditionSemantics",
            "shadowing"
        );

        LanguageProfile baseProfile = element.TryGetProperty(
            "base",
            out JsonElement baseElement
        )
            ? ParseBaseProfile(ReadString(baseElement, "profile.base"))
            : LanguageProfiles.Latest;
        LanguageProfileBuilder builder = new (baseProfile);

        ApplyEnum(
            element,
            "languageVersion",
            value => builder.WithLanguageVersion(ParseLanguageVersion(value))
        );
        ApplyEnum(
            element,
            "userDefinedFunctions",
            value => builder.WithUserDefinedFunctions(
                ParseEnum<UserDefinedFunctionsFeature>(
                    value,
                    "profile.userDefinedFunctions"
                )
            )
        );
        ApplyEnum(
            element,
            "userDefinedTypes",
            value => builder.WithUserDefinedTypes(
                ParseEnum<UserDefinedTypesFeature>(
                    value,
                    "profile.userDefinedTypes"
                )
            )
        );
        ApplyEnum(
            element,
            "recursion",
            value => builder.WithRecursion(
                ParseEnum<RecursionFeature>(value, "profile.recursion")
            )
        );

        if (element.TryGetProperty("loops", out JsonElement loopsElement))
        {
            builder.WithLoops(
                ParseFlags<LoopFeatures>(loopsElement, "profile.loops")
            );
        }

        ApplyEnum(
            element,
            "providerFunctionCalls",
            value => builder.WithProviderFunctionCalls(
                ParseEnum<ProviderFunctionCallsFeature>(
                    value,
                    "profile.providerFunctionCalls"
                )
            )
        );
        ApplyEnum(
            element,
            "openObjects",
            value => builder.WithOpenObjects(
                ParseEnum<OpenObjectsFeature>(value, "profile.openObjects")
            )
        );

        if (element.TryGetProperty("mutations", out JsonElement mutationsElement))
        {
            builder.WithMutations(
                ParseFlags<MutationFeatures>(
                    mutationsElement,
                    "profile.mutations"
                )
            );
        }

        ApplyEnum(
            element,
            "multiLevelLoopControl",
            value => builder.WithMultiLevelLoopControl(
                ParseEnum<MultiLevelLoopControlFeature>(
                    value,
                    "profile.multiLevelLoopControl"
                )
            )
        );
        ApplyEnum(
            element,
            "trailingCommas",
            value => builder.WithTrailingCommas(
                ParseEnum<TrailingCommasFeature>(
                    value,
                    "profile.trailingCommas"
                )
            )
        );
        ApplyEnum(
            element,
            "constantFolding",
            value => builder.WithConstantFolding(
                ParseEnum<ConstantFoldingFeature>(
                    value,
                    "profile.constantFolding"
                )
            )
        );
        ApplyEnum(
            element,
            "exceptionHandling",
            value => builder.WithExceptionHandling(
                ParseEnum<ExceptionHandlingFeature>(
                    value,
                    "profile.exceptionHandling"
                )
            )
        );
        ApplyEnum(
            element,
            "objectLiteralSyntax",
            value => builder.WithObjectLiteralSyntax(
                ParseEnum<ObjectLiteralSyntax>(
                    value,
                    "profile.objectLiteralSyntax"
                )
            )
        );
        ApplyEnum(
            element,
            "conditionSemantics",
            value => builder.WithConditionSemantics(
                ParseEnum<ConditionSemantics>(
                    value,
                    "profile.conditionSemantics"
                )
            )
        );

        if (element.TryGetProperty("shadowing", out JsonElement shadowingElement))
        {
            builder.WithShadowing(
                ParseFlags<ShadowingPolicy>(
                    shadowingElement,
                    "profile.shadowing"
                )
            );
        }

        return builder.Build();
    }

    private static void ParseEnvironmentDeclarations(
        JsonElement element,
        ObjectTypeGraphBuilder graph,
        IDictionary<string, ObjectTypeGraphReference> namedTypes,
        IList<ObjectTypeGraphReference> declaredTypes,
        IList<GlobalDeclaration> globals,
        IList<FunctionDeclaration> functions
    )
    {
        EnsureObject(element, "environment");
        ValidateProperties(element, "environment", "types", "globals", "functions");
        JsonElement typesElement = GetArray(element, "types", "environment");

        foreach (JsonElement typeElement in typesElement.EnumerateArray())
        {
            EnsureObject(typeElement, "environment.types[]");
            ValidateProperties(
                typeElement,
                "environment.types[]",
                "id",
                "name",
                "isOpen",
                "properties"
            );
            string id = GetRequiredString(typeElement, "id", "environment.types[]");
            string name = GetRequiredString(typeElement, "name", "environment.types[]");
            bool isOpen = GetOptionalBoolean(
                typeElement,
                "isOpen",
                "environment.types[]"
            );
            ObjectTypeGraphReference typeReference = graph.DeclareNamed(
                id,
                id,
                name,
                isOpen
            );
            namedTypes.Add(name, typeReference);
            declaredTypes.Add(typeReference);
        }

        int typeIndex = 0;

        foreach (JsonElement typeElement in typesElement.EnumerateArray())
        {
            ObjectTypeGraphReference typeReference = declaredTypes[typeIndex++];
            JsonElement propertiesElement = GetArray(
                typeElement,
                "properties",
                "environment.types[]"
            );

            foreach (JsonElement propertyElement in propertiesElement.EnumerateArray())
            {
                EnsureObject(propertyElement, "environment.types[].properties[]");
                ValidateProperties(
                    propertyElement,
                    "environment.types[].properties[]",
                    "name",
                    "type",
                    "isOptional",
                    "isReadOnly"
                );
                graph.AddProperty(
                    typeReference,
                    GetRequiredString(
                        propertyElement,
                        "name",
                        "environment.types[].properties[]"
                    ),
                    ParseType(
                        GetRequiredProperty(
                            propertyElement,
                            "type",
                            "environment.types[].properties[]"
                        ),
                        "environment.types[].properties[].type",
                        graph,
                        namedTypes
                    ),
                    GetOptionalBoolean(
                        propertyElement,
                        "isOptional",
                        "environment.types[].properties[]"
                    ),
                    GetOptionalBoolean(
                        propertyElement,
                        "isReadOnly",
                        "environment.types[].properties[]"
                    )
                );
            }
        }

        foreach (
            JsonElement globalElement in GetArray(
                element,
                "globals",
                "environment"
            ).EnumerateArray()
        )
        {
            EnsureObject(globalElement, "environment.globals[]");
            ValidateProperties(
                globalElement,
                "environment.globals[]",
                "id",
                "name",
                "type"
            );
            globals.Add(
                new GlobalDeclaration(
                    GetRequiredString(globalElement, "id", "environment.globals[]"),
                    GetRequiredString(globalElement, "name", "environment.globals[]"),
                    ParseType(
                        GetRequiredProperty(
                            globalElement,
                            "type",
                            "environment.globals[]"
                        ),
                        "environment.globals[].type",
                        graph,
                        namedTypes
                    )
                )
            );
        }

        foreach (
            JsonElement functionElement in GetArray(
                element,
                "functions",
                "environment"
            ).EnumerateArray()
        )
        {
            EnsureObject(functionElement, "environment.functions[]");
            ValidateProperties(
                functionElement,
                "environment.functions[]",
                "id",
                "name",
                "parameters",
                "returnType"
            );
            List<ParameterDeclaration> parameters = new ();

            foreach (
                JsonElement parameterElement in GetArray(
                    functionElement,
                    "parameters",
                    "environment.functions[]"
                ).EnumerateArray()
            )
            {
                EnsureObject(parameterElement, "environment.functions[].parameters[]");
                ValidateProperties(
                    parameterElement,
                    "environment.functions[].parameters[]",
                    "name",
                    "type"
                );
                parameters.Add(
                    new ParameterDeclaration(
                        GetRequiredString(
                            parameterElement,
                            "name",
                            "environment.functions[].parameters[]"
                        ),
                        ParseType(
                            GetRequiredProperty(
                                parameterElement,
                                "type",
                                "environment.functions[].parameters[]"
                            ),
                            "environment.functions[].parameters[].type",
                            graph,
                            namedTypes
                        )
                    )
                );
            }

            functions.Add(
                new FunctionDeclaration(
                    GetRequiredString(
                        functionElement,
                        "id",
                        "environment.functions[]"
                    ),
                    GetRequiredString(
                        functionElement,
                        "name",
                        "environment.functions[]"
                    ),
                    parameters,
                    ParseType(
                        GetRequiredProperty(
                            functionElement,
                            "returnType",
                            "environment.functions[]"
                        ),
                        "environment.functions[].returnType",
                        graph,
                        namedTypes
                    )
                )
            );
        }
    }

    private static ObjectTypeGraphReference ParseType(
        JsonElement element,
        string path,
        ObjectTypeGraphBuilder graph,
        IDictionary<string, ObjectTypeGraphReference> namedTypes
    )
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            string name = ReadString(element, path);

            if (namedTypes.TryGetValue(name, out ObjectTypeGraphReference? namedType))
            {
                return namedType;
            }

            return graph.From(
                name switch
                {
                    "bool" => TypeSymbols.Bool,
                    "int" => TypeSymbols.Int,
                    "float" => TypeSymbols.Float,
                    "number" => TypeSymbols.Number,
                    "string" => TypeSymbols.String,
                    "primitive" => TypeSymbols.Primitive,
                    "unknown" => TypeSymbols.Unknown,
                    "object" => TypeSymbols.Object,
                    "void" => TypeSymbols.Void,
                    "error" => TypeSymbols.ErrorValue,
                    _ => throw new FormatException(
                        $"{path} references unknown type '{name}'."
                    ),
                }
            );
        }

        EnsureObject(element, path);
        string kind = GetRequiredString(element, "kind", path);

        switch (kind)
        {
            case "nullable":
                {
                    ValidateProperties(
                        element,
                        path,
                        "kind",
                        "underlyingType"
                    );
                    return graph.Nullable(
                        ParseType(
                            GetRequiredProperty(element, "underlyingType", path),
                            $"{path}.underlyingType",
                            graph,
                            namedTypes
                        )
                    );
                }

            case "array":
                {
                    ValidateProperties(
                        element,
                        path,
                        "kind",
                        "elementType",
                        "readOnly"
                    );
                    ObjectTypeGraphReference elementType = ParseType(
                        GetRequiredProperty(element, "elementType", path),
                        $"{path}.elementType",
                        graph,
                        namedTypes
                    );
                    return GetOptionalBoolean(element, "readOnly", path)
                        ? graph.ReadOnlyArray(elementType)
                        : graph.Array(elementType);
                }

            default:
                throw new FormatException(
                    $"{path}.kind must be 'nullable' or 'array'."
                );
        }
    }

    private static LanguageProfile ParseBaseProfile(string value)
    {
        return value switch
        {
            "latest" => LanguageProfiles.Latest,
            "1" => LanguageProfiles.Version1,
            "1.1" => LanguageProfiles.Version1_1,
            "1.2" => LanguageProfiles.Version1_2,
            _ => throw new FormatException(
                "profile.base must be 'latest', '1', '1.1', or '1.2'."
            ),
        };
    }

    private static LanguageVersion ParseLanguageVersion(JsonElement element)
    {
        return ReadString(element, "profile.languageVersion") switch
        {
            "1" => LanguageVersion.Version1,
            "1.1" => LanguageVersion.Version1_1,
            "1.2" => LanguageVersion.Version1_2,
            _ => throw new FormatException(
                "profile.languageVersion must be '1', '1.1', or '1.2'."
            ),
        };
    }

    private static T ParseEnum<T>(JsonElement element, string path)
        where T : struct, Enum
    {
        string value = ReadString(element, path);

        if (Enum.TryParse(value, true, out T result) && Enum.IsDefined(result))
        {
            return result;
        }

        throw new FormatException($"{path} has unsupported value '{value}'.");
    }

    private static T ParseFlags<T>(JsonElement element, string path)
        where T : struct, Enum
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"{path} must be an array.");
        }

        long result = 0;

        foreach (JsonElement valueElement in element.EnumerateArray())
        {
            T value = ParseEnum<T>(valueElement, $"{path}[]");
            result |= Convert.ToInt64(value);
        }

        return (T)Enum.ToObject(typeof(T), result);
    }

    private static void ApplyEnum(
        JsonElement element,
        string propertyName,
        Action<JsonElement> apply
    )
    {
        if (element.TryGetProperty(propertyName, out JsonElement value))
        {
            apply(value);
        }
    }

    private static JsonElement GetArray(
        JsonElement element,
        string propertyName,
        string path
    )
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            using JsonDocument emptyArray = JsonDocument.Parse("[]");
            return emptyArray.RootElement.Clone();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"{path}.{propertyName} must be an array.");
        }

        return value;
    }

    private static JsonElement GetRequiredProperty(
        JsonElement element,
        string propertyName,
        string path
    )
    {
        if (element.TryGetProperty(propertyName, out JsonElement value))
        {
            return value;
        }

        throw new FormatException($"{path}.{propertyName} is required.");
    }

    private static string GetRequiredString(
        JsonElement element,
        string propertyName,
        string path
    )
    {
        return ReadString(
            GetRequiredProperty(element, propertyName, path),
            $"{path}.{propertyName}"
        );
    }

    private static bool GetOptionalBoolean(
        JsonElement element,
        string propertyName,
        string path
    )
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        throw new FormatException($"{path}.{propertyName} must be a boolean.");
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"{path} must be a string.");
        }

        return element.GetString()!;
    }

    private static void EnsureObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"{path} must be an object.");
        }
    }

    private static void ValidateProperties(
        JsonElement element,
        string path,
        params string[] propertyNames
    )
    {
        ISet<string> allowedProperties = propertyNames.ToHashSet(
            StringComparer.Ordinal
        );

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                throw new FormatException(
                    $"{path} contains unsupported property '{property.Name}'."
                );
            }
        }
    }

    private sealed record GlobalDeclaration(
        string Id,
        string Name,
        ObjectTypeGraphReference Type
    );

    private sealed record ParameterDeclaration(
        string Name,
        ObjectTypeGraphReference Type
    );

    private sealed record FunctionDeclaration(
        string Id,
        string Name,
        IReadOnlyList<ParameterDeclaration> Parameters,
        ObjectTypeGraphReference ReturnType
    );
}
