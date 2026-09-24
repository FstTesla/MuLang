using MuLang.Core;

namespace MuLang.TestSupport;

public static class TestLanguageProfileFactory
{
    public static LanguageProfile Create(
        UserDefinedFunctionsFeature? userDefinedFunctions = null,
        RecursionFeature? recursion = null,
        LoopFeatures? loops = null,
        ProviderFunctionCallsFeature? providerFunctionCalls = null,
        OpenObjectsFeature? openObjects = null,
        MutationFeatures? mutations = null,
        MultiLevelLoopControlFeature? multiLevelLoopControl = null,
        TrailingCommasFeature? trailingCommas = null,
        ConstantFoldingFeature? constantFolding = null,
        ConditionSemantics? conditionSemantics = null,
        ShadowingPolicy? shadowing = null
    )
    {
        LanguageProfileBuilder builder = new (LanguageProfiles.Version1);

        if (userDefinedFunctions is not null)
        {
            builder.WithUserDefinedFunctions(userDefinedFunctions.Value);
        }

        if (recursion is not null)
        {
            builder.WithRecursion(recursion.Value);
        }

        if (loops is not null)
        {
            builder.WithLoops(loops.Value);
        }

        if (providerFunctionCalls is not null)
        {
            builder.WithProviderFunctionCalls(providerFunctionCalls.Value);
        }

        if (openObjects is not null)
        {
            builder.WithOpenObjects(openObjects.Value);
        }

        if (mutations is not null)
        {
            builder.WithMutations(mutations.Value);
        }

        if (multiLevelLoopControl is not null)
        {
            builder.WithMultiLevelLoopControl(multiLevelLoopControl.Value);
        }

        if (trailingCommas is not null)
        {
            builder.WithTrailingCommas(trailingCommas.Value);
        }

        if (constantFolding is not null)
        {
            builder.WithConstantFolding(constantFolding.Value);
        }

        if (conditionSemantics is not null)
        {
            builder.WithConditionSemantics(conditionSemantics.Value);
        }

        if (shadowing is not null)
        {
            builder.WithShadowing(shadowing.Value);
        }

        return builder.Build();
    }
}
