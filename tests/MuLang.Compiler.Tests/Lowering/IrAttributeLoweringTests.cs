using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;
using MuLang.IR.Serialization;

namespace MuLang.Compiler.Tests.Lowering;

public sealed class IrAttributeLoweringTests
{
    [Test]
    public void EmitsNamesWithoutChangingExecutableIdentity()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "globalValue", TypeSymbols.Int)
            .AddFunction(
                "function.next",
                "next",
                [ new ParameterSymbol("value", TypeSymbols.Int) ],
                TypeSymbols.Int
            )
            .Build(LanguageVersion.Version1_2);
        const string sourceText = """
                                  type First { value: int };
                                  type Second { value: int };

                                  func read(item: First): int {
                                      var result = item.value;
                                      return result;
                                  }

                                  var second: Second = { value: int = 1 };
                                  return read(second) + globalValue + next(1);
                                  """;
        CompilationResult result = MuLangCompiler.Compile(
            sourceText,
            environment,
            CompilationMode.Program,
            TypeSymbols.Int,
            LanguageProfiles.Version1_2
        );
        IrProgram program = result.Program ??
            throw new AssertionException("Expected an IR program.");
        SourceText source = SourceText.From(sourceText);
        IrFunction function = program.UserFunctions.Single();
        IReadOnlyList<string> slotNames =
        [
            .. function.Slots
                .Select(
                    slot => program.Attributes.TryGetSlot(
                        function.Id,
                        slot.Id,
                        out IrSlotAttributes? attributes
                    )
                        ? attributes.SourceName
                        : null
                )
                .OfType<string>(),
        ];
        string muir = MuIrWriter.WriteToString(program);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics.HasErrors, Is.False);
            Assert.That(function.Id, Is.EqualTo("user:0"));
            Assert.That(
                program.Attributes.Functions[function.Id].SourceName,
                Is.EqualTo("read")
            );
            Assert.That(
                source.GetText(
                    program.Attributes.Functions[function.Id]
                        .DeclarationSpan
                        .GetValueOrDefault()
                ),
                Is.EqualTo(
                    """
                    func read(item: First): int {
                        var result = item.value;
                        return result;
                    }
                    """
                )
            );
            Assert.That(slotNames, Is.EquivalentTo([ "item", "result" ]));
            Assert.That(
                source.GetText(
                    program.Attributes.Slots[
                        new IrSlotAttributeTarget(function.Id, 0)
                    ].DeclarationSpan.GetValueOrDefault()
                ),
                Is.EqualTo("item: First")
            );
            Assert.That(
                source.GetText(
                    program.Attributes.Slots.Values.Single(
                        static attributes => attributes.SourceName == "result"
                    ).DeclarationSpan.GetValueOrDefault()
                ),
                Is.EqualTo("var result")
            );
            Assert.That(
                source.GetText(
                    program.Attributes.Slots.Values.Single(
                        static attributes => attributes.SourceName == "second"
                    ).DeclarationSpan.GetValueOrDefault()
                ),
                Is.EqualTo("var second: Second")
            );
            Assert.That(
                program.Attributes.Program?.ProviderGlobalNames,
                Is.EqualTo(
                    [ new IrProviderSymbolName("global.value", "globalValue") ]
                )
            );
            Assert.That(
                program.Attributes.Program?.ProviderFunctionNames,
                Is.EqualTo(
                    [ new IrProviderSymbolName("function.next", "next") ]
                )
            );
            Assert.That(
                program.Attributes.Types.Values
                    .SelectMany(static attributes => attributes.SourceNames),
                Is.EquivalentTo([ "First", "Second" ])
            );
            Assert.That(
                function.Slots.Count - slotNames.Count,
                Is.GreaterThan(0)
            );
            Assert.That(
                muir,
                Does.Contain(
                    "attribute type "
                ).And.Contain(
                    "source-type-names { 2 [\"First\", \"Second\"] }"
                )
            );
        }
    }

    [Test]
    public void EmitsCatchParameterDeclarationSpan()
    {
        const string sourceText = """
                                  try {
                                      throw { code = "failure", message = "Failure." };
                                  } catch (failure) {
                                      throw;
                                  }
                                  """;
        CompilationResult result = MuLangCompiler.Compile(
            sourceText,
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            CompilationMode.Program,
            profile: LanguageProfiles.Version1_2
        );
        IrProgram program = result.Program ??
            throw new AssertionException("Expected an IR program.");
        IrSlotAttributes failure = program.Attributes.Slots.Values.Single(
            static attributes => attributes.SourceName == "failure"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics.HasErrors, Is.False);
            Assert.That(
                SourceText.From(sourceText).GetText(
                    failure.DeclarationSpan.GetValueOrDefault()
                ),
                Is.EqualTo("(failure)")
            );
            Assert.That(program.Attributes.Program?.SourceDocument, Is.Null);
        }
    }
}
