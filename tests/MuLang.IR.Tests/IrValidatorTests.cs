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
                [ CreateTemporarySlot(0, TypeSymbols.Int) ],
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
                        new IrTerminator.Return(default, 0),
                        0
                    ),
                ],
                [ new IrLifetimeRegion(0, null, 0) ],
                [ ]
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
                CreateTemporarySlot(0, readOnlyType),
                CreateTemporarySlot(1, mutableType),
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
                CreateTemporarySlot(0, readOnlyType),
                CreateTemporarySlot(1, mutableType),
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
                CreateTemporarySlot(0, TypeSymbols.Int),
                CreateTemporarySlot(1, TypeSymbols.Float),
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
                CreateTemporarySlot(0, TypeSymbols.Number),
                CreateTemporarySlot(1, TypeSymbols.Float),
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
                CreateTemporarySlot(0, TypeSymbols.Number),
                CreateTemporarySlot(1, TypeSymbols.Int),
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
                CreateTemporarySlot(0, readOnlyType),
                CreateTemporarySlot(1, TypeSymbols.Int),
                CreateTemporarySlot(2, TypeSymbols.Int),
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
                CreateTemporarySlot(0, TypeSymbols.Int),
                CreateTemporarySlot(1, mutableType),
                CreateTemporarySlot(2, TypeSymbols.Bool),
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
                CreateTemporarySlot(2, TypeSymbols.String),
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
                    new IrTerminator.Return(default, 2),
                    0
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
                        new IrTerminator.Resume(default),
                        0
                    ),
                ],
                [ new IrLifetimeRegion(0, null, 0) ],
                [ ]
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
                        new IrTerminator.Return(default, resultSlot),
                        0
                    ),
                ],
                [ new IrLifetimeRegion(0, null, 0) ],
                [ ]
            ),
            [ ]
        );
    }

    private static IrSlot CreateTemporarySlot(int id, TypeSymbol type) =>
        new (
            id,
            IrSlotKind.Temporary,
            type,
            null,
            IrSlotMutability.Mutable,
            0
        );
}
