using MuLang.Compiler.Binding;
using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Tests.Binding;

public sealed class BinderTests
{
    [Test]
    public void ResolvesGlobalsAndNumericOperators()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        BindingResult result = BindExpression("value + 1.5", environment);
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Value.Type, Is.SameAs(TypeSymbols.Float));
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsUndefinedVariables()
    {
        BindingResult result = BindExpression("missing", CreateEmptyEnvironment());

        AssertDiagnostic(result, DiagnosticCodes.UndefinedName);
    }

    [Test]
    public void ResolvesFunctionCallsAndValidatesArguments()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction(
                "function.format",
                "format",
                [ new ParameterSymbol("value", TypeSymbols.Int) ],
                TypeSymbols.String
            )
            .Build();
        BindingResult valid = BindExpression("format(1)", environment);
        BindingResult invalid = BindExpression("format(\"value\", 2)", environment);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid.Diagnostics, Is.Empty);
            AssertDiagnostic(invalid, DiagnosticCodes.TypeMismatch);
            AssertDiagnostic(invalid, DiagnosticCodes.ArgumentCountMismatch);
        }
    }

    [Test]
    public void InfersArrayLiteralElementType()
    {
        BindingResult result = BindExpression("[1, 2.5]", CreateEmptyEnvironment());
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Array array = (BoundExpression.Array)root.Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(array.ArrayType.ElementType, Is.SameAs(TypeSymbols.Float));
            Assert.That(array.Elements[0], Is.TypeOf<BoundExpression.Conversion>());
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void UsesExpectedTypeForEmptyArrayLiteral()
    {
        BindingResult result = BindProgram(
            "var values: int[] = [];",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void InfersReadOnlyArrayLiteralType()
    {
        BindingResult result = BindExpression(
            "$[1, 2]",
            CreateEmptyEnvironment(LanguageVersion.Version2),
            profile: LanguageProfiles.Version2
        );
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Array array = (BoundExpression.Array)root.Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(array.ArrayType.IsReadOnly, Is.True);
            Assert.That(array.ArrayType.ElementType, Is.SameAs(TypeSymbols.Int));
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void UsesReadOnlyExpectedTypeForEmptyReadOnlyLiteral()
    {
        BindingResult result = BindProgram(
            "var values: int[]$ = $[];",
            CreateEmptyEnvironment(LanguageVersion.Version2),
            TypeSymbols.Void,
            LanguageProfiles.Version2
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void RejectsReadOnlyLiteralInMutableExpectedContext()
    {
        BindingResult result = BindProgram(
            "var values: int[] = $[1];",
            CreateEmptyEnvironment(LanguageVersion.Version2),
            TypeSymbols.Void,
            LanguageProfiles.Version2
        );

        AssertDiagnostic(result, DiagnosticCodes.TypeMismatch);
    }

    [Test]
    public void ConvertsMutableLiteralToReadOnlyExpectedType()
    {
        BindingResult result = BindProgram(
            "var values: number[]$ = [1, 2];",
            CreateEmptyEnvironment(LanguageVersion.Version2),
            TypeSymbols.Void,
            LanguageProfiles.Version2
        );
        BoundRoot.Program root = (BoundRoot.Program)result.Root;
        BoundStatement.VariableDeclaration declaration =
            (BoundStatement.VariableDeclaration)root.Statements[0];

        Assert.That(
            declaration.Initializer,
            Is.TypeOf<BoundExpression.Conversion>()
        );
    }

    [Test]
    public void FindsCovariantReadOnlyConditionalArrayType()
    {
        BindingResult result = BindExpression(
            "true ? [1] : $[2.0]",
            CreateEmptyEnvironment(LanguageVersion.Version2),
            profile: LanguageProfiles.Version2
        );
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;

        Assert.That(
            TypeRelations.AreEquivalent(
                root.Value.Type,
                TypeSymbols.ReadOnlyArray(TypeSymbols.Number)
            ),
            Is.True
        );
    }

    [Test]
    public void RejectsElementAssignmentThroughReadOnlyView()
    {
        BindingResult result = BindProgram(
            "var values: int[]$ = [1]; values[0] = 2;",
            CreateEmptyEnvironment(LanguageVersion.Version2),
            TypeSymbols.Void,
            LanguageProfiles.Version2
        );

        AssertDiagnostic(result, DiagnosticCodes.ReadOnlyTarget);
    }

    [Test]
    public void AdoptsExpectedMutableArrayTypeAndConvertsElements()
    {
        BindingResult result = BindProgram(
            "var values: number[] = [1, 2];",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );
        BoundRoot.Program root = (BoundRoot.Program)result.Root;
        BoundStatement.VariableDeclaration declaration =
            (BoundStatement.VariableDeclaration)root.Statements[0];
        BoundExpression.Array array =
            RequireInitializer<BoundExpression.Array>(declaration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(array.ArrayType.ElementType, Is.SameAs(TypeSymbols.Number));
            Assert.That(array.Elements, Has.All.TypeOf<BoundExpression.Conversion>());
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsNullOnlyArrayWithoutThrowing()
    {
        BindingResult result = BindExpression("[null]", CreateEmptyEnvironment());

        AssertDiagnostic(result, DiagnosticCodes.CannotInferType);
    }

    [Test]
    public void InfersNullableArrayFromMixedNullElements()
    {
        BindingResult result = BindExpression("[1, null]", CreateEmptyEnvironment());
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Array array = (BoundExpression.Array)root.Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.AreEquivalent(
                    array.ArrayType.ElementType,
                    TypeSymbols.Nullable(TypeSymbols.Int)
                ),
                Is.True
            );
            Assert.That(array.Elements, Has.All.TypeOf<BoundExpression.Conversion>());
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsVoidArrayElementWithoutThrowing()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.log", "log", [ ], TypeSymbols.Void)
            .Build();
        BindingResult result = BindExpression("[log()]", environment);

        AssertDiagnostic(result, DiagnosticCodes.InvalidVoidExpression);
    }

    [Test]
    public void AcceptsNullElementsWithNullableExpectedType()
    {
        BindingResult result = BindProgram(
            "var values: int?[] = [null, 1];",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ReportsEmptyArrayWithoutExpectedType()
    {
        BindingResult result = BindExpression("[]", CreateEmptyEnvironment());

        AssertDiagnostic(result, DiagnosticCodes.CannotInferType);
    }

    [Test]
    public void ResolvesKnownAndDynamicProperties()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            true,
            [ new ObjectPropertySymbol("id", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .Build();
        BindingResult known = BindExpression("item.id", environment);
        BindingResult dynamic = BindExpression("item.extra", environment);
        BoundRoot.Expression knownRoot = (BoundRoot.Expression)known.Root;
        BoundRoot.Expression dynamicRoot = (BoundRoot.Expression)dynamic.Root;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownRoot.Value.Type, Is.SameAs(TypeSymbols.Int));
            Assert.That(
                TypeRelations.AreEquivalent(
                    dynamicRoot.Value.Type,
                    TypeSymbols.Nullable(TypeSymbols.Unknown)
                ),
                Is.True
            );
            Assert.That(known.Diagnostics, Is.Empty);
            Assert.That(dynamic.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsDuplicateObjectProperties()
    {
        BindingResult result = BindExpression(
            "{ value: 1, value: 2 }",
            CreateEmptyEnvironment()
        );

        AssertDiagnostic(result, DiagnosticCodes.DuplicateObjectProperty);
    }

    [Test]
    public void InfersOptionalObjectLiteralProperties()
    {
        BindingResult result = BindExpression(
            "{ value?: 1 }",
            CreateEmptyEnvironment()
        );
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Object objectExpression =
            (BoundExpression.Object)root.Value;
        ObjectPropertySymbol property = objectExpression.ObjectType.Properties.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(property.IsOptional, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ExpectedObjectTypeDeterminesLiteralPropertyOptionality()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .Build();
        BindingResult result = BindProgram(
            "var item: Item = { value?: 1 };",
            environment,
            TypeSymbols.Void
        );
        BoundRoot.Program root = (BoundRoot.Program)result.Root;
        BoundStatement.VariableDeclaration declaration =
            (BoundStatement.VariableDeclaration)root.Statements[0];
        BoundExpression.Object objectExpression =
            RequireInitializer<BoundExpression.Object>(declaration);
        ObjectPropertySymbol property = objectExpression.ObjectType.Properties.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(property.IsOptional, Is.False);
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ValidatesUnaryAndBinaryOperators()
    {
        BindingResult valid = BindExpression("~1 & 3", CreateEmptyEnvironment());
        BindingResult invalid = BindExpression("\"a\" - \"b\"", CreateEmptyEnvironment());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(valid.Diagnostics, Is.Empty);
            AssertDiagnostic(invalid, DiagnosticCodes.OperatorNotDefined);
        }
    }

    [Test]
    public void RequiresConcreteIntForBitwiseOperators()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Number)
            .Build();
        BindingResult result = BindExpression("value & 1", environment);

        AssertDiagnostic(result, DiagnosticCodes.OperatorNotDefined);
    }

    [Test]
    public void SupportsBooleanEagerOperators()
    {
        BindingResult andResult = BindExpression(
            "true & false",
            CreateEmptyEnvironment()
        );
        BindingResult orResult = BindExpression(
            "true | false",
            CreateEmptyEnvironment()
        );
        BindingResult xorResult = BindExpression(
            "true ^ false",
            CreateEmptyEnvironment()
        );
        BindingResult mixedResult = BindExpression(
            "true & 1",
            CreateEmptyEnvironment()
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                ((BoundRoot.Expression)andResult.Root).Value.Type,
                Is.SameAs(TypeSymbols.Bool)
            );
            Assert.That(orResult.Diagnostics, Is.Empty);
            Assert.That(xorResult.Diagnostics, Is.Empty);
            AssertDiagnostic(mixedResult, DiagnosticCodes.OperatorNotDefined);
        }
    }

    [Test]
    public void ValidatesCheckedConversions()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Number)
            .Build();
        BindingResult intCast = BindExpression("value as int", environment);
        BindingResult floatCast = BindExpression("value as float", environment);
        BindingResult invalidNumericCast = BindExpression(
            "1.0 as int",
            CreateEmptyEnvironment()
        );
        BindingResult invalidNumericPromotion = BindExpression(
            "1 as float",
            CreateEmptyEnvironment()
        );
        BindingResult invalidStringCast = BindExpression(
            "1 as string",
            CreateEmptyEnvironment()
        );
        BindingResult invalidObjectCast = BindExpression(
            "{} as string",
            CreateEmptyEnvironment()
        );
        BoundExpression.Conversion intConversion = (BoundExpression.Conversion)
            ((BoundRoot.Expression)intCast.Root).Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(intCast.Diagnostics, Is.Empty);
            Assert.That(floatCast.Diagnostics, Is.Empty);
            Assert.That(intConversion.IsCast, Is.True);
            AssertDiagnostic(invalidNumericCast, DiagnosticCodes.InvalidConversion);
            AssertDiagnostic(invalidNumericPromotion, DiagnosticCodes.InvalidConversion);
            AssertDiagnostic(invalidStringCast, DiagnosticCodes.InvalidConversion);
            AssertDiagnostic(invalidObjectCast, DiagnosticCodes.InvalidConversion);
        }
    }

    [Test]
    public void MaterializesImplicitConversions()
    {
        BindingResult result = BindExpression(
            "1",
            CreateEmptyEnvironment(),
            TypeSymbols.Number
        );
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Conversion conversion =
            (BoundExpression.Conversion)root.Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversion.ConversionKind, Is.EqualTo(ConversionKind.Implicit));
            Assert.That(conversion.IsCast, Is.False);
            Assert.That(conversion.Type, Is.SameAs(TypeSymbols.Number));
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsUseBeforeDefiniteAssignment()
    {
        BindingResult result = BindProgram(
            "var value: int; return value;",
            CreateEmptyEnvironment(),
            TypeSymbols.Int
        );

        AssertDiagnostic(result, DiagnosticCodes.UnassignedLocal);
    }

    [Test]
    public void IntersectsDefiniteAssignmentAcrossBranches()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        BindingResult partial = BindProgram(
            "var value: int; if (condition) value = 1; return value;",
            environment,
            TypeSymbols.Int
        );
        BindingResult complete = BindProgram(
            "var value: int; if (condition) value = 1; else value = 2; return value;",
            environment,
            TypeSymbols.Int
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(partial, DiagnosticCodes.UnassignedLocal);
            Assert.That(complete.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void RejectsLocalShadowing()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        BindingResult result = BindProgram(
            "var value = 1;",
            environment,
            TypeSymbols.Void
        );

        AssertDiagnostic(result, DiagnosticCodes.ShadowedVariable);
    }

    [Test]
    public void RequiresBlocksAroundEmbeddedDeclarations()
    {
        BindingResult result = BindProgram(
            "if (true) var value = 1;",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        AssertDiagnostic(result, DiagnosticCodes.InvalidEmbeddedDeclaration);
    }

    [Test]
    public void ValidatesProgramReturnPaths()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        BindingResult complete = BindProgram(
            "if (condition) return 1; else return 2;",
            environment,
            TypeSymbols.Int
        );
        BindingResult incomplete = BindProgram(
            "if (condition) return 1;",
            environment,
            TypeSymbols.Int
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(complete.Diagnostics, Is.Empty);
            AssertDiagnostic(incomplete, DiagnosticCodes.NotAllPathsReturn);
        }
    }

    [Test]
    public void RecognizesNonCompletingConstantLoops()
    {
        BindingResult returningLoop = BindProgram(
            "while (true) return 1;",
            CreateEmptyEnvironment(),
            TypeSymbols.Int
        );
        BindingResult unreachableAfterLoop = BindProgram(
            "while (true) { } return 1;",
            CreateEmptyEnvironment(),
            TypeSymbols.Int
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                returningLoop.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Not.Contain(DiagnosticCodes.NotAllPathsReturn)
            );
            AssertDiagnostic(unreachableAfterLoop, DiagnosticCodes.UnreachableStatement);
        }
    }

    [Test]
    public void IgnoresUnreachableBreaksWhenAnalyzingInfiniteLoops()
    {
        BindingResult result = BindProgram(
            "while (true) { return 1; break; }",
            CreateEmptyEnvironment(),
            TypeSymbols.Int
        );

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Not.Contain(DiagnosticCodes.NotAllPathsReturn)
        );
    }

    [Test]
    public void MergesContinuePathsBeforeForIterator()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .AddGlobal("global.count", "count", TypeSymbols.Int)
            .Build();
        BindingResult result = BindProgram(
            """
            var value: int;
            for (var index = 0; index < count; index = value) {
                if (condition)
                    continue;
                value = 1;
            }
            """,
            environment,
            TypeSymbols.Void
        );

        AssertDiagnostic(result, DiagnosticCodes.UnassignedLocal);
    }

    [Test]
    public void DoesNotAnalyzeUnreachableForIteratorAsReachable()
    {
        BindingResult result = BindProgram(
            "var value: int; for (;; value = value) break;",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Not.Contain(DiagnosticCodes.UnassignedLocal)
        );
    }

    [Test]
    public void ValidatesLoopControlStatements()
    {
        BindingResult invalidBreak = BindProgram(
            "break;",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );
        BindingResult validBreak = BindProgram(
            "while (true) break;",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(invalidBreak, DiagnosticCodes.BreakOutsideLoop);
            Assert.That(validBreak.Diagnostics, Is.Empty);
        }
    }

    [TestCase("while (true) break 0;")]
    [TestCase("while (true) break -1;")]
    [TestCase("while (true) break 2;")]
    [TestCase("while (true) continue 0;")]
    [TestCase("while (true) continue 2;")]
    public void ValidatesLoopControlLevels(string source)
    {
        BindingResult result = BindProgram(
            source,
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        AssertDiagnostic(result, DiagnosticCodes.InvalidLoopLevel);
    }

    [Test]
    public void AllowsLoopControlLevelWithinNestingDepth()
    {
        BindingResult result = BindProgram(
            "while (true) while (true) break 2;",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void AllowsRemovalOnlyForOptionalOrDynamicProperties()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [
                new ObjectPropertySymbol("required", TypeSymbols.Int),
                new ObjectPropertySymbol("optional", TypeSymbols.Int, true),
            ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .Build();
        BindingResult result = BindProgram(
            "item.optional~; item.required~;",
            environment,
            TypeSymbols.Void
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.Diagnostics.Count(
                    static diagnostic =>
                        diagnostic.Code == DiagnosticCodes.PropertyNotRemovable
                ),
                Is.EqualTo(1)
            );
        }
    }

    [Test]
    public void RejectsAssignmentToArrayLength()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Array(TypeSymbols.Int)
            )
            .Build();
        BindingResult result = BindProgram(
            "values.length = 0;",
            environment,
            TypeSymbols.Void
        );

        AssertDiagnostic(result, DiagnosticCodes.ReadOnlyTarget);
    }

    [Test]
    public void UsesExpectedStructuredObjectType()
    {
        ObjectTypeSymbol pointType = new (
            "type.point",
            "Point",
            false,
            [
                new ObjectPropertySymbol("x", TypeSymbols.Number),
                new ObjectPropertySymbol("label", TypeSymbols.String, true),
            ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(pointType)
            .Build();
        BindingResult result = BindProgram(
            "var point: Point = { x: 1 };",
            environment,
            TypeSymbols.Void
        );
        BoundRoot.Program root = (BoundRoot.Program)result.Root;
        BoundStatement.VariableDeclaration declaration =
            (BoundStatement.VariableDeclaration)root.Statements[0];
        BoundExpression.Object objectExpression =
            RequireInitializer<BoundExpression.Object>(declaration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(objectExpression.ObjectType, Is.SameAs(pointType));
            Assert.That(
                objectExpression.Properties[0].Value,
                Is.TypeOf<BoundExpression.Conversion>()
            );
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void AllowsNullableOpenObjectAdditionalProperties()
    {
        ObjectTypeSymbol openType = new (
            "type.open",
            "Open",
            true,
            [ ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(openType)
            .AddGlobal(
                "global.value",
                "value",
                TypeSymbols.Nullable(TypeSymbols.Int)
            )
            .Build();
        BindingResult result = BindProgram(
            "var item: Open = @{ extra: value };",
            environment,
            TypeSymbols.Void
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ReportsUnrelatedConditionalBranchTypes()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        BindingResult result = BindExpression(
            "condition ? 1 : \"value\"",
            environment
        );

        AssertDiagnostic(result, DiagnosticCodes.TypeMismatch);
    }

    [Test]
    public void ConvertsConditionalBranchesToTheirCommonType()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        BindingResult result = BindExpression(
            "condition ? 1 : 2.5",
            environment
        );
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Conditional conditional =
            (BoundExpression.Conditional)root.Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conditional.Type, Is.SameAs(TypeSymbols.Float));
            Assert.That(conditional.WhenTrue, Is.TypeOf<BoundExpression.Conversion>());
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void InfersNullCoalescingCommonTypeFromNonNullLeftValue()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.value",
                "value",
                TypeSymbols.Nullable(TypeSymbols.Int)
            )
            .Build();
        BindingResult result = BindExpression("value ?? 2.5", environment);
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Coalescing coalescing =
            (BoundExpression.Coalescing)root.Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coalescing.Type, Is.SameAs(TypeSymbols.Float));
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void PreservesNullabilityWhenNullCoalescingRightOperandIsNullable()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.value",
                "value",
                TypeSymbols.Nullable(TypeSymbols.Int)
            )
            .Build();
        BindingResult result = BindExpression("value ?? null", environment);
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Coalescing coalescing =
            (BoundExpression.Coalescing)root.Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.AreEquivalent(
                    coalescing.Type,
                    TypeSymbols.Nullable(TypeSymbols.Int)
                ),
                Is.True
            );
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsNonNullableNullCoalescingLeftOperand()
    {
        BindingResult result = BindExpression(
            "1 ?? 2",
            new EnvironmentBuilder().Build()
        );

        AssertDiagnostic(result, DiagnosticCodes.OperatorNotDefined);
    }

    [Test]
    public void ReportsNonLiteralNullCoalescingLeftOperand()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        BindingResult result = BindExpression(
            "(condition ? null : null) ?? 2",
            environment
        );

        AssertDiagnostic(result, DiagnosticCodes.OperatorNotDefined);
    }

    [Test]
    public void ReportsContextualNullCoalescingTypeMismatch()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.value",
                "value",
                TypeSymbols.Nullable(TypeSymbols.Int)
            )
            .Build();
        BindingResult result = BindExpression(
            "value ?? 1",
            environment,
            TypeSymbols.String
        );

        AssertDiagnostic(result, DiagnosticCodes.TypeMismatch);
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Message),
            Has.None.Contains("have no common result type")
        );
    }

    [Test]
    public void NormalizesNullableOperatorOperandsWithCheckedConversions()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.value",
                "value",
                TypeSymbols.Nullable(TypeSymbols.Int)
            )
            .Build();
        BindingResult result = BindExpression("value + 1", environment);
        BoundRoot.Expression root = (BoundRoot.Expression)result.Root;
        BoundExpression.Binary binary = (BoundExpression.Binary)root.Value;
        BoundExpression.Conversion conversion =
            (BoundExpression.Conversion)binary.Left;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conversion.ConversionKind, Is.EqualTo(ConversionKind.Checked));
            Assert.That(conversion.Type, Is.SameAs(TypeSymbols.Int));
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsMissingRequiredObjectProperties()
    {
        ObjectTypeSymbol pointType = new (
            "type.point",
            "Point",
            false,
            [ new ObjectPropertySymbol("x", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(pointType)
            .Build();
        BindingResult result = BindProgram(
            "var point: Point = {};",
            environment,
            TypeSymbols.Void
        );

        AssertDiagnostic(result, DiagnosticCodes.MissingObjectProperty);
    }

    [Test]
    public void DoesNotThrowForMalformedSources()
    {
        const string alphabet = "abc012+-*/%{}[]()?:;,.~@\"=<>!&|_ \n";
        string[] keywords =
        [
            "as",
            "bool",
            "break",
            "continue",
            "else",
            "false",
            "for",
            "has",
            "if",
            "int",
            "is",
            "null",
            "number",
            "object",
            "return",
            "string",
            "true",
            "unknown",
            "var",
            "while",
        ];
        Random random = new (481516);
        EnvironmentSchema environment = CreateEmptyEnvironment();

        for (int sourceIndex = 0; sourceIndex < 500; sourceIndex++)
        {
            string source;

            if (sourceIndex % 2 == 0)
            {
                int length = random.Next(0, 40);
                char[] characters = new char[length];

                for (int characterIndex = 0; characterIndex < length; characterIndex++)
                {
                    characters[characterIndex] = alphabet[random.Next(alphabet.Length)];
                }

                source = new string(characters);
            }
            else
            {
                int tokenCount = random.Next(0, 15);
                string[] tokens = new string[tokenCount];

                for (int tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
                {
                    tokens[tokenIndex] = keywords[random.Next(keywords.Length)];
                }

                source = string.Join(' ', tokens);
            }

            SyntaxTree expressionTree = Parser.Parse(
                SourceText.From(source),
                CompilationMode.Expression
            );
            SyntaxTree programTree = Parser.Parse(
                SourceText.From(source),
                CompilationMode.Program
            );

            Assert.DoesNotThrow(() => Binder.Bind(expressionTree, environment));
            Assert.DoesNotThrow(() => Binder.Bind(programTree, environment));
            Assert.DoesNotThrow(
                () => Binder.Bind(programTree, environment, TypeSymbols.Int)
            );
        }
    }

    private static BindingResult BindExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType = null,
        LanguageProfile? profile = null
    )
    {
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Expression,
            profile ?? LanguageProfiles.Version1
        );

        return Binder.Bind(syntaxTree, environment, expectedType);
    }

    private static BindingResult BindProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType,
        LanguageProfile? profile = null
    )
    {
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Program,
            profile ?? LanguageProfiles.Version1
        );

        return Binder.Bind(syntaxTree, environment, resultType);
    }

    private static EnvironmentSchema CreateEmptyEnvironment(
        LanguageVersion languageVersion = LanguageVersion.Version1
    )
    {
        return new EnvironmentBuilder().Build(languageVersion);
    }

    private static void AssertDiagnostic(BindingResult result, string code)
    {
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(code)
        );
    }

    private static TExpression RequireInitializer<TExpression>(
        BoundStatement.VariableDeclaration declaration
    )
        where TExpression : BoundExpression
    {
        return declaration.Initializer as TExpression ??
            throw new AssertionException(
                $"Expected initializer of type '{typeof(TExpression).Name}'."
            );
    }
}
