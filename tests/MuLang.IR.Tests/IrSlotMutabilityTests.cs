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
                CreateSlot(
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

    [Test]
    public void AcceptsReadOnlyLocalWithoutDefinitionSite()
    {
        IrProgram program = CreateProgram(
            [
                CreateSlot(
                    0,
                    IrSlotKind.Local,
                    TypeSymbols.Int,
                    "value",
                    IrSlotMutability.ReadOnly
                ),
            ],
            [ ],
            TypeSymbols.Void,
            null
        );

        Assert.That(Validate(program), Is.Empty);
    }

    [Test]
    public void RejectsRepeatedReadOnlyLocalDefinitionOnOnePath()
    {
        IrProgram program = CreateProgram(
            [
                CreateSlot(
                    0,
                    IrSlotKind.Local,
                    TypeSymbols.Int,
                    "value",
                    IrSlotMutability.ReadOnly
                ),
            ],
            [
                new IrInstruction.Constant(default, 0, TypeSymbols.Int, 1L),
                new IrInstruction.Constant(default, 0, TypeSymbols.Int, 2L),
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
                CreateSlot(
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
    public void LegacyConstructorDefaultsParametersToReadOnly()
    {
#pragma warning disable CS0618
        IrSlot parameter = new (
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value"
        );
#pragma warning restore CS0618

        Assert.That(parameter.Mutability, Is.EqualTo(IrSlotMutability.ReadOnly));
    }

    [Test]
    public void AcceptsReadOnlyParameterWithoutDefinitionSite()
    {
        IrSlot parameter = CreateSlot(
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value",
            IrSlotMutability.ReadOnly
        );

        Assert.That(
            Validate(CreateProgramWithUserParameter(parameter, [ ])),
            Is.Empty
        );
    }

    [Test]
    public void RejectsMutableParameter()
    {
        IrSlot parameter = CreateSlot(
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
        IrSlot parameter = CreateSlot(
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value",
            IrSlotMutability.ReadOnly
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
    public void RejectsReadOnlyDefinitionSiteInLoop()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Program,
            LanguageProfiles.Version1_1.Fingerprint,
            CreateRootFunction(
                "$entry",
                TypeSymbols.Void,
                0,
                [
                    CreateSlot(
                        0,
                        IrSlotKind.Local,
                        TypeSymbols.Int,
                        "value",
                        IrSlotMutability.ReadOnly
                    ),
                    CreateSlot(
                        1,
                        IrSlotKind.Temporary,
                        TypeSymbols.Bool,
                        null,
                        IrSlotMutability.Mutable
                    ),
                ],
                [
                    CreateRootBlock(
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
                    CreateRootBlock(
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
                    CreateRootBlock(
                        2,
                        [ ],
                        new IrTerminator.Return(default, null)
                    ),
                ]
            ),
            [ ]
        );

        Assert.That(
            IrValidator.Validate(program, environment)
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidSlot)
        );
    }

    [Test]
    public void AcceptsReadOnlyDefinitionsOnExclusiveBranches()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Program,
            LanguageProfiles.Version1_1.Fingerprint,
            CreateRootFunction(
                "$entry",
                TypeSymbols.Void,
                0,
                [
                    CreateSlot(
                        0,
                        IrSlotKind.Local,
                        TypeSymbols.Int,
                        "value",
                        IrSlotMutability.ReadOnly
                    ),
                    CreateSlot(
                        1,
                        IrSlotKind.Temporary,
                        TypeSymbols.Bool,
                        null,
                        IrSlotMutability.Mutable
                    ),
                ],
                [
                    CreateRootBlock(
                        0,
                        [
                            new IrInstruction.Constant(
                                default,
                                1,
                                TypeSymbols.Bool,
                                true
                            ),
                        ],
                        new IrTerminator.Branch(default, 1, 1, 2)
                    ),
                    CreateRootBlock(
                        1,
                        [
                            new IrInstruction.Constant(
                                default,
                                0,
                                TypeSymbols.Int,
                                1L
                            ),
                        ],
                        new IrTerminator.Jump(default, 3)
                    ),
                    CreateRootBlock(
                        2,
                        [
                            new IrInstruction.Constant(
                                default,
                                0,
                                TypeSymbols.Int,
                                2L
                            ),
                        ],
                        new IrTerminator.Jump(default, 3)
                    ),
                    CreateRootBlock(
                        3,
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
            CreateRootFunction(
                "$entry",
                resultType,
                0,
                slots,
                [
                    CreateRootBlock(
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
            CreateRootFunction(
                "$entry",
                TypeSymbols.Void,
                0,
                [ ],
                [
                    CreateRootBlock(
                        0,
                        [ ],
                        new IrTerminator.Return(default, null)
                    ),
                ]
            ),
            [
                CreateRootFunction(
                    "user",
                    TypeSymbols.Void,
                    0,
                    [ parameter ],
                    [
                        CreateRootBlock(
                            0,
                            instructions,
                            new IrTerminator.Return(default, null)
                        ),
                    ]
                ),
            ]
        );
    }

    private static IrSlot CreateSlot(
        int id,
        IrSlotKind kind,
        TypeSymbol type,
        string? name,
        IrSlotMutability mutability
    ) =>
        new (id, kind, type, name, mutability, 0);

    private static IrBasicBlock CreateRootBlock(
        int id,
        IReadOnlyCollection<IrInstruction> instructions,
        IrTerminator terminator
    ) =>
        new (id, instructions, terminator, 0);

    private static IrFunction CreateRootFunction(
        string id,
        TypeSymbol returnType,
        int entryBlock,
        IReadOnlyList<IrSlot> slots,
        IReadOnlyList<IrBasicBlock> blocks
    ) =>
        new (
            id,
            returnType,
            entryBlock,
            slots,
            blocks,
            [ new IrLifetimeRegion(0, null, entryBlock) ],
            [ ]
        );
}
