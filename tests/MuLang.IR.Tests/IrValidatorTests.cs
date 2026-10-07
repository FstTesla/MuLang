using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.IR.Tests;

public sealed class IrValidatorTests
{
    [Test]
    public void RejectsUnexpectedCompilationMetadata()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Expression,
            LanguageProfiles.Version1.Fingerprint,
            new IrFunction(
                "$entry",
                TypeSymbols.Int,
                0,
                [ new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null) ],
                [
                    new IrBasicBlock(
                        0,
                        [
                            new IrInstruction.Constant(
                                default,
                                0,
                                TypeSymbols.Int,
                                1L
                            ),
                        ],
                        new IrTerminator.Return(default, 0)
                    ),
                ]
            ),
            [ ]
        );

        Assert.That(
            IrValidator.Validate(
                program,
                environment,
                CompilationMode.Program,
                program.LanguageProfileFingerprint
            ).Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.CompilationMetadataMismatch)
        );
    }

    [Test]
    public void RejectsUncheckedReadOnlyCapabilityAcquisition()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(LanguageVersion.Version1_1);
        ArrayTypeSymbol readOnlyType = TypeSymbols.ReadOnlyArray(TypeSymbols.Int);
        ArrayTypeSymbol mutableType = TypeSymbols.Array(TypeSymbols.Int);
        IrProgram program = CreateProgram(
            environment,
            mutableType,
            [
                new IrSlot(0, IrSlotKind.Temporary, readOnlyType, null),
                new IrSlot(1, IrSlotKind.Temporary, mutableType, null),
            ],
            [
                new IrInstruction.CreateArray(default, 0, readOnlyType, [ ]),
                new IrInstruction.Convert(
                    default,
                    1,
                    0,
                    mutableType,
                    IrConversionKind.ValueConversion
                ),
            ],
            1
        );

        Assert.That(
            IrValidator.Validate(program, environment)
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.TypeMismatch)
        );
    }

    [Test]
    public void AcceptsCheckedReadOnlyCapabilityAcquisition()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(LanguageVersion.Version1_1);
        ArrayTypeSymbol readOnlyType = TypeSymbols.ReadOnlyArray(TypeSymbols.Int);
        ArrayTypeSymbol mutableType = TypeSymbols.Array(TypeSymbols.Int);
        IrProgram program = CreateProgram(
            environment,
            mutableType,
            [
                new IrSlot(0, IrSlotKind.Temporary, readOnlyType, null),
                new IrSlot(1, IrSlotKind.Temporary, mutableType, null),
            ],
            [
                new IrInstruction.CreateArray(default, 0, readOnlyType, [ ]),
                new IrInstruction.Convert(
                    default,
                    1,
                    0,
                    mutableType,
                    IrConversionKind.CheckedCast
                ),
            ],
            1
        );

        Assert.That(IrValidator.Validate(program, environment), Is.Empty);
    }

    [Test]
    public void RejectsTransformingCheckedCast()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = CreateProgram(
            environment,
            TypeSymbols.Float,
            [
                new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null),
                new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Float, null),
            ],
            [
                new IrInstruction.Constant(default, 0, TypeSymbols.Int, 1L),
                new IrInstruction.Convert(
                    default,
                    1,
                    0,
                    TypeSymbols.Float,
                    IrConversionKind.CheckedCast
                ),
            ],
            1
        );

        Assert.That(
            IrValidator.Validate(program, environment)
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.TypeMismatch)
        );
    }

    [Test]
    public void AcceptsRuntimeRepresentationCast()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = CreateProgram(
            environment,
            TypeSymbols.Float,
            [
                new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Number, null),
                new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Float, null),
            ],
            [
                new IrInstruction.Constant(default, 0, TypeSymbols.Number, 1.0),
                new IrInstruction.Convert(
                    default,
                    1,
                    0,
                    TypeSymbols.Float,
                    IrConversionKind.CheckedCast
                ),
            ],
            1
        );

        Assert.That(IrValidator.Validate(program, environment), Is.Empty);
    }

    [Test]
    public void RejectsCastOnlyPairAsValueConversion()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = CreateProgram(
            environment,
            TypeSymbols.Int,
            [
                new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Number, null),
                new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Int, null),
            ],
            [
                new IrInstruction.Constant(default, 0, TypeSymbols.Number, 1L),
                new IrInstruction.Convert(
                    default,
                    1,
                    0,
                    TypeSymbols.Int,
                    IrConversionKind.ValueConversion
                ),
            ],
            1
        );

        Assert.That(
            IrValidator.Validate(program, environment)
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.TypeMismatch)
        );
    }

    [Test]
    public void RejectsElementWriteThroughReadOnlyArraySlot()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(LanguageVersion.Version1_1);
        ArrayTypeSymbol readOnlyType = TypeSymbols.ReadOnlyArray(TypeSymbols.Int);
        IrProgram program = CreateProgram(
            environment,
            TypeSymbols.Void,
            [
                new IrSlot(0, IrSlotKind.Temporary, readOnlyType, null),
                new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Int, null),
                new IrSlot(2, IrSlotKind.Temporary, TypeSymbols.Int, null),
            ],
            [
                new IrInstruction.CreateArray(default, 0, readOnlyType, [ ]),
                new IrInstruction.Constant(default, 1, TypeSymbols.Int, 0L),
                new IrInstruction.Constant(default, 2, TypeSymbols.Int, 1L),
                new IrInstruction.SetElement(default, 0, 1, 2, false),
            ],
            null
        );

        Assert.That(
            IrValidator.Validate(program, environment)
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.TypeMismatch)
        );
    }

    [Test]
    public void AcceptsCovariantReadOnlyProviderArguments()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction(
                "function.accept",
                "accept",
                [
                    new ParameterSymbol(
                        "values",
                        TypeSymbols.ReadOnlyArray(TypeSymbols.Number)
                    ),
                ],
                TypeSymbols.Bool
            )
            .Build(LanguageVersion.Version1_1);
        ArrayTypeSymbol mutableType = TypeSymbols.Array(TypeSymbols.Int);
        IrProgram program = CreateProgram(
            environment,
            TypeSymbols.Bool,
            [
                new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null),
                new IrSlot(1, IrSlotKind.Temporary, mutableType, null),
                new IrSlot(2, IrSlotKind.Temporary, TypeSymbols.Bool, null),
            ],
            [
                new IrInstruction.Constant(default, 0, TypeSymbols.Int, 1L),
                new IrInstruction.CreateArray(default, 1, mutableType, [ 0 ]),
                new IrInstruction.ProviderCall(
                    default,
                    2,
                    "function.accept",
                    TypeSymbols.Bool,
                    [ 1 ]
                ),
            ],
            2
        );

        Assert.That(IrValidator.Validate(program, environment), Is.Empty);
    }

    [Test]
    public void AcceptsHandlerErrorSlotAtExceptionalEntry()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(LanguageVersion.Version1_2);
        IrFunction entry = new (
            "$entry",
            TypeSymbols.String,
            0,
            [
                new IrSlot(
                    0,
                    IrSlotKind.Local,
                    TypeSymbols.ErrorValue,
                    "error",
                    IrSlotMutability.ReadOnly,
                    1
                ),
                new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.String, null, IrSlotMutability.Mutable, 1),
                new IrSlot(2, IrSlotKind.Temporary, TypeSymbols.String, null),
            ],
            [
                new IrBasicBlock(
                    0,
                    [
                        new IrInstruction.Constant(
                            default,
                            2,
                            TypeSymbols.String,
                            "ok"
                        ),
                    ],
                    new IrTerminator.Return(default, 2)
                ),
                new IrBasicBlock(
                    1,
                    [
                        new IrInstruction.GetProperty(
                            default,
                            1,
                            0,
                            "code",
                            false,
                            false
                        ),
                    ],
                    new IrTerminator.Return(default, 1),
                    1
                ),
            ],
            [
                new IrLifetimeRegion(0, null, 0),
                new IrLifetimeRegion(1, 0, 1),
            ],
            [
                new IrExceptionRegion(
                    0,
                    null,
                    null,
                    new IrExceptionProtectedRegion(0, [ 0 ]),
                    new IrExceptionHandler(1, [ 1 ], 0),
                    null
                ),
            ]
        );
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Expression,
            LanguageProfiles.Version1_2.Fingerprint,
            entry,
            [ ]
        );

        Assert.That(IrValidator.Validate(program, environment), Is.Empty);
    }

    [Test]
    public void RejectsResumeOutsideCleanup()
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
                [ ],
                [
                    new IrBasicBlock(
                        0,
                        [ ],
                        new IrTerminator.Resume(default)
                    ),
                ]
            ),
            [ ]
        );

        Assert.That(
            IrValidator.Validate(program, environment)
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidStructure)
        );
    }

    private static IrProgram CreateProgram(
        EnvironmentSchema environment,
        TypeSymbol resultType,
        IReadOnlyList<IrSlot> slots,
        IReadOnlyList<IrInstruction> instructions,
        int? resultSlot
    )
    {
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
                        new IrTerminator.Return(default, resultSlot)
                    ),
                ]
            ),
            [ ]
        );
    }
}
