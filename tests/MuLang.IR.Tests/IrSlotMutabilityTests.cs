using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.IR.Tests;

public sealed class IrSlotMutabilityTests
{
    [Test]
    public void AcceptsOneReadOnlyLocalDefinitionSite()
    {
        IrProgram program = CreateProgram(
            [
                new IrSlot(
                    0,
                    IrSlotKind.Local,
                    TypeSymbols.Int,
                    "value",
                    IrSlotMutability.ReadOnly
                ),
            ],
            [
                new IrInstruction.Constant(default, 0, TypeSymbols.Int, 1L),
            ],
            TypeSymbols.Int,
            0
        );

        Assert.That(Validate(program), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(2)]
    public void RejectsInvalidReadOnlyLocalDefinitionCount(int count)
    {
        IrProgram program = CreateProgram(
            [
                new IrSlot(
                    0,
                    IrSlotKind.Local,
                    TypeSymbols.Int,
                    "value",
                    IrSlotMutability.ReadOnly
                ),
            ],
            [
                .. Enumerable.Range(0, count)
                    .Select(
                        static value => new IrInstruction.Constant(
                            default,
                            0,
                            TypeSymbols.Int,
                            (long)value
                        )
                    ),
            ],
            TypeSymbols.Void,
            null
        );

        Assert.That(
            Validate(program).Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidSlot)
        );
    }

    [Test]
    public void RejectsReadOnlyTemporarySlots()
    {
        IrProgram program = CreateProgram(
            [
                new IrSlot(
                    0,
                    IrSlotKind.Temporary,
                    TypeSymbols.Int,
                    "value",
                    IrSlotMutability.ReadOnly
                ),
            ],
            [
                new IrInstruction.Constant(default, 0, TypeSymbols.Int, 1L),
            ],
            TypeSymbols.Int,
            0
        );

        Assert.That(
            Validate(program).Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidSlot)
        );
    }

    [Test]
    public void ParametersDefaultToReadOnly()
    {
        IrSlot parameter = new (
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value"
        );

        Assert.That(parameter.Mutability, Is.EqualTo(IrSlotMutability.ReadOnly));
    }

    [Test]
    public void AcceptsReadOnlyParameterWithoutDefinitionSite()
    {
        IrSlot parameter = new (
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value"
        );

        Assert.That(
            Validate(CreateProgramWithUserParameter(parameter, [ ])),
            Is.Empty
        );
    }

    [Test]
    public void RejectsMutableParameter()
    {
        IrSlot parameter = new (
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value",
            IrSlotMutability.Mutable
        );

        Assert.That(
            Validate(CreateProgramWithUserParameter(parameter, [ ]))
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidSlot)
        );
    }

    [Test]
    public void RejectsParameterDefinitionSite()
    {
        IrSlot parameter = new (
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value"
        );

        Assert.That(
            Validate(
                CreateProgramWithUserParameter(
                    parameter,
                    [
                        new IrInstruction.Constant(
                            default,
                            0,
                            TypeSymbols.Int,
                            1L
                        ),
                    ]
                )
            ).Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidSlot)
        );
    }

    [Test]
    public void AcceptsReadOnlyDefinitionSiteInLoop()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Program,
            LanguageProfiles.Version1_1.Fingerprint,
            new IrFunction(
                "$entry",
                TypeSymbols.Void,
                0,
                [
                    new IrSlot(
                        0,
                        IrSlotKind.Local,
                        TypeSymbols.Int,
                        "value",
                        IrSlotMutability.ReadOnly
                    ),
                    new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Bool, null),
                ],
                [
                    new IrBasicBlock(
                        0,
                        [
                            new IrInstruction.Constant(
                                default,
                                1,
                                TypeSymbols.Bool,
                                true
                            ),
                        ],
                        new IrTerminator.Jump(default, 1)
                    ),
                    new IrBasicBlock(
                        1,
                        [
                            new IrInstruction.Constant(
                                default,
                                0,
                                TypeSymbols.Int,
                                1L
                            ),
                        ],
                        new IrTerminator.Branch(default, 1, 1, 2)
                    ),
                    new IrBasicBlock(
                        2,
                        [ ],
                        new IrTerminator.Return(default, null)
                    ),
                ]
            ),
            [ ]
        );

        Assert.That(IrValidator.Validate(program, environment), Is.Empty);
    }

    private static IReadOnlyList<Diagnostic> Validate(
        IrProgram program
    )
    {
        return IrValidator.Validate(program, new EnvironmentBuilder().Build());
    }

    private static IrProgram CreateProgram(
        IReadOnlyList<IrSlot> slots,
        IReadOnlyList<IrInstruction> instructions,
        TypeSymbol resultType,
        int? result
    )
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        return new IrProgram(
            environment.Fingerprint,
            CompilationMode.Expression,
            LanguageProfiles.Version1_1.Fingerprint,
            new IrFunction(
                "$entry",
                resultType,
                0,
                slots,
                [
                    new IrBasicBlock(
                        0,
                        instructions,
                        new IrTerminator.Return(default, result)
                    ),
                ]
            ),
            [ ]
        );
    }

    private static IrProgram CreateProgramWithUserParameter(
        IrSlot parameter,
        IReadOnlyList<IrInstruction> instructions
    )
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        return new IrProgram(
            environment.Fingerprint,
            CompilationMode.Program,
            LanguageProfiles.Version1_1.Fingerprint,
            new IrFunction(
                "$entry",
                TypeSymbols.Void,
                0,
                [ ],
                [
                    new IrBasicBlock(
                        0,
                        [ ],
                        new IrTerminator.Return(default, null)
                    ),
                ]
            ),
            [
                new IrFunction(
                    "user",
                    TypeSymbols.Void,
                    0,
                    [ parameter ],
                    [
                        new IrBasicBlock(
                            0,
                            instructions,
                            new IrTerminator.Return(default, null)
                        ),
                    ]
                ),
            ]
        );
    }
}
