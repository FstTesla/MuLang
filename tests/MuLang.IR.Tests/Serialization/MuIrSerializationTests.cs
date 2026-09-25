using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR.Serialization;
using System.Text;

namespace MuLang.IR.Tests.Serialization;

public sealed class MuIrSerializationTests
{
    [Test]
    public void CompleteProgramRoundTripsCanonically()
    {
        IrProgram program = CreateCompleteProgram();

        string text = MuIrWriter.WriteToString(program);
        MuIrReadResult result = MuIrReader.Read(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Program, Is.Not.Null);
            Assert.That(MuIrWriter.WriteToString(result.Program!), Is.EqualTo(text));
            Assert.That(text, Does.StartWith("muir 1\n"));
            Assert.That(text, Does.EndWith("end\n"));
            Assert.That(text, Does.Not.Contain("\r"));
        }

        IReadOnlyCollection<IrInstruction> instructions =
            result.Program!.EntryFunction.Blocks[0].Instructions;

        Assert.That(
            instructions.Select(static instruction => instruction.GetType()),
            Is.EquivalentTo(
                CreateCompleteProgram().EntryFunction.Blocks[0].Instructions
                    .Select(static instruction => instruction.GetType())
            )
        );

        double negativeZero = (double)instructions
            .OfType<IrInstruction.Constant>()
            .Single(
                static value =>
                    value.Value is double number &&
                    BitConverter.DoubleToInt64Bits(number) == long.MinValue
            )
            .Value!;
        Assert.That(BitConverter.DoubleToInt64Bits(negativeZero), Is.EqualTo(long.MinValue));
    }

    [TestCase("minimal.muir")]
    [TestCase("complete.muir")]
    public void CanonicalFixtureRemainsStable(string fileName)
    {
        string path = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Serialization",
            "Fixtures",
            fileName
        );
        string text = File.ReadAllText(path);

        MuIrReadResult result = MuIrReader.Read(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(MuIrWriter.WriteToString(result.Program!), Is.EqualTo(text));
        }
    }

    [Test]
    public void ReaderAcceptsCommentsBlankLinesAndCrLf()
    {
        string canonical = MuIrWriter.WriteToString(CreateMinimalProgram());
        string decorated = $"# MuIR fixture\r\n\r\n{canonical.Replace("\n", "\r\n")}# end\r\n";

        MuIrReadResult result = MuIrReader.Read(decorated);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(
                MuIrWriter.WriteToString(result.Program!),
                Is.EqualTo(canonical)
            );
        }
    }

    [Test]
    public void StreamWriterUsesUtf8WithoutBomAndHonorsLeaveOpen()
    {
        using MemoryStream stream = new ();

        MuIrWriter.Write(CreateMinimalProgram(), stream);

        byte[] bytes = stream.ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream.CanWrite, Is.True);
            Assert.That(bytes, Is.Not.Empty);
            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(Encoding.UTF8.GetString(bytes), Does.StartWith("muir 1\n"));
        }
    }

    [Test]
    public void StreamReaderReportsInvalidUtf8AndHonorsLeaveOpen()
    {
        using MemoryStream stream = new ([ 0xC3, 0x28 ]);

        MuIrReadResult result = MuIrReader.Read(stream);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Program, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(MuIrDiagnosticCodes.InvalidUtf8));
            Assert.That(stream.CanRead, Is.True);
        }
    }

    [TestCase("nope 1", MuIrDiagnosticCodes.InvalidMagic)]
    [TestCase("muir 2", MuIrDiagnosticCodes.UnsupportedVersion)]
    [TestCase(
        "muir 1 mode expression environment \"e\" profile \"p\" types 0 entry",
        MuIrDiagnosticCodes.UnexpectedToken
    )]
    [TestCase(
        "muir 1 mode expression environment \"e\" profile \"p\" types 0 " +
        "entry \"f\" t0 bb0 slots 0 blocks 0 { } users 0 end",
        MuIrDiagnosticCodes.UndefinedReference
    )]
    public void ReaderRejectsMalformedDocuments(string text, string expectedCode)
    {
        MuIrReadResult result = MuIrReader.Read(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Program, Is.Null);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(expectedCode));
            Assert.That(result.Diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(result.Diagnostics[0].Line, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.Diagnostics[0].Column, Is.GreaterThanOrEqualTo(1));
        }
    }

    [Test]
    public void ReaderRejectsTrailingContent()
    {
        MuIrReadResult result = MuIrReader.Read(
            $"{MuIrWriter.WriteToString(CreateMinimalProgram())}extra"
        );

        Assert.That(
            result.Diagnostics.Single().Code,
            Is.EqualTo(MuIrDiagnosticCodes.TrailingContent)
        );
    }

    [Test]
    public void ReaderEnforcesDocumentAndCollectionLimits()
    {
        string text = MuIrWriter.WriteToString(CreateMinimalProgram());
        MuIrReadResult documentResult = MuIrReader.Read(
            text,
            new MuIrReaderOptions(maximumDocumentLength: text.Length - 1)
        );
        MuIrReadResult typeResult = MuIrReader.Read(
            text,
            new MuIrReaderOptions(maximumTypes: 1, maximumFunctions: 1)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                documentResult.Diagnostics.Single().Code,
                Is.EqualTo(MuIrDiagnosticCodes.LimitExceeded)
            );
            Assert.That(typeResult.Success, Is.True);
        }
    }

    [Test]
    public void ReaderEnforcesEveryStructuralLimit()
    {
        string text = MuIrWriter.WriteToString(CreateCompleteProgram());
        IReadOnlyList<MuIrReaderOptions> limits =
        [
            new (maximumStringLength: 2),
            new (maximumTokenLength: 3),
            new (maximumTypeNestingDepth: 1),
            new (maximumTypes: 1),
            new (maximumFunctions: 1),
            new (maximumSlotsPerFunction: 1),
            new (maximumBlocksPerFunction: 1),
            new (maximumInstructionsPerBlock: 1),
            new (maximumListElements: 1),
        ];

        Assert.That(
            limits.Select(options => MuIrReader.Read(text, options))
                .Select(static result => result.Diagnostics.Single().Code),
            Is.All.EqualTo(MuIrDiagnosticCodes.LimitExceeded)
        );
    }

    [TestCase("constant", "unknown-op", MuIrDiagnosticCodes.InvalidInstruction)]
    [TestCase("0 1 %0", "2147483647 1 %0", MuIrDiagnosticCodes.InvalidSpan)]
    [TestCase("int 42", "int 9223372036854775808", MuIrDiagnosticCodes.NumericOverflow)]
    [TestCase("\"env\"", "\"\\q\"", MuIrDiagnosticCodes.InvalidEscape)]
    public void ReaderReportsSpecificMalformedConstructs(
        string oldValue,
        string newValue,
        string expectedCode
    )
    {
        string text = MuIrWriter.WriteToString(CreateMinimalProgram())
            .Replace(oldValue, newValue, StringComparison.Ordinal);

        MuIrReadResult result = MuIrReader.Read(text);

        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(expectedCode));
    }

    [Test]
    public void WriterRejectsNonPortableValuesAndErrorTypes()
    {
        IrProgram unsupportedValue = CreateMinimalProgram(
            TypeSymbols.Unknown,
            new object()
        );
        IrProgram errorType = CreateMinimalProgram(TypeSymbols.Error, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                () => MuIrWriter.WriteToString(unsupportedValue),
                Throws.TypeOf<MuIrSerializationException>()
            );
            Assert.That(
                () => MuIrWriter.WriteToString(errorType),
                Throws.TypeOf<MuIrSerializationException>()
            );
        }
    }

    [Test]
    public void ReaderOptionsRequirePositiveLimits()
    {
        Assert.That(
            static () => new MuIrReaderOptions(maximumTypes: 0),
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );
    }

    [Test]
    public void EquivalentCompositeInstancesShareCanonicalTypeEntries()
    {
        ArrayTypeSymbol shared = TypeSymbols.Array(TypeSymbols.Int);
        IrProgram sharedProgram = CreateTypeOnlyProgram([ shared, shared ]);
        IrProgram distinctProgram = CreateTypeOnlyProgram(
            [
                TypeSymbols.Array(TypeSymbols.Int),
                TypeSymbols.Array(TypeSymbols.Int),
            ]
        );

        Assert.That(
            MuIrWriter.WriteToString(distinctProgram),
            Is.EqualTo(MuIrWriter.WriteToString(sharedProgram))
        );
    }

    [Test]
    public void PropertyOrderDoesNotAffectCanonicalOutput()
    {
        ObjectTypeSymbol first = ObjectTypeSymbol.CreateAnonymous(
            false,
            [
                new ObjectPropertySymbol("zeta", TypeSymbols.String),
                new ObjectPropertySymbol("alpha", TypeSymbols.Int),
            ]
        );
        ObjectTypeSymbol second = ObjectTypeSymbol.CreateAnonymous(
            false,
            [
                new ObjectPropertySymbol("alpha", TypeSymbols.Int),
                new ObjectPropertySymbol("zeta", TypeSymbols.String),
            ]
        );

        Assert.That(
            MuIrWriter.WriteToString(CreateTypeOnlyProgram([ first ])),
            Is.EqualTo(MuIrWriter.WriteToString(CreateTypeOnlyProgram([ second ])))
        );
    }

    [Test]
    public void ProviderObjectIdentityDoesNotAffectCanonicalOutput()
    {
        ObjectTypeSymbol first = new (
            "provider.first",
            "First",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        ObjectTypeSymbol second = new (
            "provider.second",
            "Second",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );

        Assert.That(
            MuIrWriter.WriteToString(CreateTypeOnlyProgram([ first ])),
            Is.EqualTo(MuIrWriter.WriteToString(CreateTypeOnlyProgram([ second ])))
        );
    }

    [Test]
    public void SelfRecursiveObjectRoundTripsWithForwardReference()
    {
        ObjectTypeGraphBuilder builder = new ();
        ObjectTypeGraphReference node = builder.DeclareAnonymous("node", false);
        builder.AddProperty(node, "next", builder.Nullable(node));
        ObjectTypeSymbol type = (ObjectTypeSymbol)builder.Build()[node];
        string text = MuIrWriter.WriteToString(CreateTypeOnlyProgram([ type ]));

        MuIrReadResult result = MuIrReader.Read(text);
        ObjectTypeSymbol reconstructed = (ObjectTypeSymbol)result.Program!.Slots[0].Type;
        NullableTypeSymbol next =
            (NullableTypeSymbol)reconstructed.Properties.Single().Type;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(reconstructed.Id, Is.Null);
            Assert.That(next.UnderlyingType, Is.SameAs(reconstructed));
            Assert.That(MuIrWriter.WriteToString(result.Program), Is.EqualTo(text));
        }
    }

    [Test]
    public void MutuallyRecursiveObjectsRoundTripCanonically()
    {
        ObjectTypeGraphBuilder builder = new ();
        ObjectTypeGraphReference first = builder.DeclareNamed(
            "first",
            "type.first",
            "First",
            false
        );
        ObjectTypeGraphReference second = builder.DeclareNamed(
            "second",
            "type.second",
            "Second",
            false
        );
        builder.AddProperty(first, "second", second);
        builder.AddProperty(second, "first", first);
        IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> graph =
            builder.Build();
        string text = MuIrWriter.WriteToString(
            CreateTypeOnlyProgram([ graph[first], graph[second] ])
        );

        MuIrReadResult result = MuIrReader.Read(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(MuIrWriter.WriteToString(result.Program!), Is.EqualTo(text));
            Assert.That(
                TypeRelations.AreEquivalent(
                    graph[first],
                    result.Program!.Slots[0].Type
                ),
                Is.True
            );
        }
    }

    [Test]
    public void BisimilarRecursiveCycleExpansionsSerializeIdentically()
    {
        ObjectTypeSymbol selfRecursive = CreateRecursiveCycle(1);
        ObjectTypeSymbol twoNodeCycle = CreateRecursiveCycle(2);

        Assert.That(
            MuIrWriter.WriteToString(CreateTypeOnlyProgram([ twoNodeCycle ])),
            Is.EqualTo(
                MuIrWriter.WriteToString(CreateTypeOnlyProgram([ selfRecursive ]))
            )
        );
    }

    [Test]
    public void ReadOnlyLocalMutabilityRoundTrips()
    {
        IrProgram program = CreateMinimalProgram();
        IrSlot source = program.EntryFunction.Slots[0];
        IrSlot readOnly = new (
            source.Id,
            IrSlotKind.Local,
            source.Type,
            "value",
            IrSlotMutability.ReadOnly
        );
        program = program with
        {
            EntryFunction = program.EntryFunction with
            {
                Slots = [ readOnly ],
            },
        };

        MuIrReadResult result = MuIrReader.Read(MuIrWriter.WriteToString(program));

        Assert.That(
            result.Program!.Slots.Single().Mutability,
            Is.EqualTo(IrSlotMutability.ReadOnly)
        );
    }

    [TestCase(IrUnaryOperator.Identity, "identity")]
    [TestCase(IrUnaryOperator.Negate, "negate")]
    [TestCase(IrUnaryOperator.LogicalNot, "logical-not")]
    [TestCase(IrUnaryOperator.BitwiseNot, "bitwise-not")]
    public void UnaryOperatorTokensRoundTrip(
        IrUnaryOperator value,
        string token
    )
    {
        IrInstruction.Unary instruction = new (
            default,
            0,
            value,
            0
        );

        AssertInstructionToken(instruction, token);
    }

    [TestCase(IrBinaryOperator.Add, "add")]
    [TestCase(IrBinaryOperator.Subtract, "subtract")]
    [TestCase(IrBinaryOperator.Multiply, "multiply")]
    [TestCase(IrBinaryOperator.Divide, "divide")]
    [TestCase(IrBinaryOperator.Remainder, "remainder")]
    [TestCase(IrBinaryOperator.LeftShift, "left-shift")]
    [TestCase(IrBinaryOperator.RightShift, "right-shift")]
    [TestCase(IrBinaryOperator.LessThan, "less-than")]
    [TestCase(IrBinaryOperator.LessThanOrEqual, "less-than-or-equal")]
    [TestCase(IrBinaryOperator.GreaterThan, "greater-than")]
    [TestCase(IrBinaryOperator.GreaterThanOrEqual, "greater-than-or-equal")]
    [TestCase(IrBinaryOperator.StructuralEqual, "structural-equal")]
    [TestCase(IrBinaryOperator.StructuralNotEqual, "structural-not-equal")]
    [TestCase(IrBinaryOperator.IdentityEqual, "identity-equal")]
    [TestCase(IrBinaryOperator.IdentityNotEqual, "identity-not-equal")]
    [TestCase(IrBinaryOperator.BitwiseAnd, "bitwise-and")]
    [TestCase(IrBinaryOperator.BitwiseXor, "bitwise-xor")]
    [TestCase(IrBinaryOperator.BitwiseOr, "bitwise-or")]
    public void BinaryOperatorTokensRoundTrip(
        IrBinaryOperator value,
        string token
    )
    {
        IrInstruction.Binary instruction = new (
            default,
            0,
            value,
            0,
            0
        );

        AssertInstructionToken(instruction, token);
    }

    [TestCase(IrConversionKind.ValueConversion, "value")]
    [TestCase(IrConversionKind.CheckedCast, "checked")]
    public void ConversionKindTokensRoundTrip(
        IrConversionKind value,
        string token
    )
    {
        IrInstruction.Convert instruction = new (
            default,
            0,
            0,
            TypeSymbols.Int,
            value
        );

        AssertInstructionToken(instruction, token);
    }

    private static void AssertInstructionToken(
        IrInstruction instruction,
        string token
    )
    {
        IrProgram program = CreateInstructionProgram(instruction);
        string text = MuIrWriter.WriteToString(program);
        MuIrReadResult result = MuIrReader.Read(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain($" {token}"));
            Assert.That(result.Success, Is.True);
            Assert.That(MuIrWriter.WriteToString(result.Program!), Is.EqualTo(text));
        }
    }

    private static IrProgram CreateCompleteProgram()
    {
        NullableTypeSymbol nullableInt = TypeSymbols.Nullable(TypeSymbols.Int);
        ArrayTypeSymbol mutableArray = TypeSymbols.Array(TypeSymbols.Int);
        ArrayTypeSymbol readOnlyArray = TypeSymbols.ReadOnlyArray(TypeSymbols.Number);
        ObjectTypeSymbol namedObject = new (
            "provider.sample",
            "Sample",
            true,
            [
                new ObjectPropertySymbol("value", TypeSymbols.Int),
                new ObjectPropertySymbol("label", TypeSymbols.String, true),
            ]
        );
        ObjectTypeSymbol anonymousObject = ObjectTypeSymbol.CreateAnonymous(
            false,
            [ new ObjectPropertySymbol("items", readOnlyArray) ]
        );
        IReadOnlyList<IrSlot> slots =
        [
            new (0, IrSlotKind.Local, TypeSymbols.Int, "input"),
            new (1, IrSlotKind.Local, TypeSymbols.Bool, "flag"),
            new (2, IrSlotKind.Temporary, nullableInt, null),
            new (3, IrSlotKind.Temporary, TypeSymbols.Float, null),
            new (4, IrSlotKind.Temporary, TypeSymbols.String, null),
            new (5, IrSlotKind.Temporary, mutableArray, null),
            new (6, IrSlotKind.Temporary, namedObject, null),
            new (7, IrSlotKind.Temporary, anonymousObject, null),
            new (8, IrSlotKind.Temporary, TypeSymbols.Unknown, null),
            new (9, IrSlotKind.Temporary, TypeSymbols.Number, null),
            new (10, IrSlotKind.Temporary, TypeSymbols.Object, null),
        ];
        IReadOnlyCollection<IrInstruction> instructions =
        [
            new IrInstruction.Constant(new TextSpan(0, 1), 1, TypeSymbols.Bool, true),
            new IrInstruction.Constant(new TextSpan(1, 1), 0, TypeSymbols.Int, 42L),
            new IrInstruction.Constant(new TextSpan(2, 1), 2, nullableInt, null),
            new IrInstruction.Constant(new TextSpan(3, 1), 3, TypeSymbols.Float, -0.0),
            new IrInstruction.Constant(new TextSpan(4, 1), 3, TypeSymbols.Float, double.NaN),
            new IrInstruction.Constant(new TextSpan(5, 1), 3, TypeSymbols.Float, double.PositiveInfinity),
            new IrInstruction.Constant(new TextSpan(6, 1), 3, TypeSymbols.Float, double.NegativeInfinity),
            new IrInstruction.Constant(new TextSpan(7, 1), 3, TypeSymbols.Float, 1.2345678901234567),
            new IrInstruction.Constant(new TextSpan(8, 1), 4, TypeSymbols.String, "a\n\"b\"\u0001"),
            new IrInstruction.Copy(new TextSpan(8, 1), 9, 0),
            new IrInstruction.LoadGlobal(new TextSpan(9, 1), 0, "global.value"),
            new IrInstruction.Unary(new TextSpan(10, 1), 9, IrUnaryOperator.Negate, 0),
            new IrInstruction.Binary(new TextSpan(11, 1), 9, IrBinaryOperator.Add, 0, 0),
            new IrInstruction.Convert(new TextSpan(12, 1), 9, 0, TypeSymbols.Number, IrConversionKind.ValueConversion),
            new IrInstruction.Truthiness(new TextSpan(13, 1), 1, 8),
            new IrInstruction.TypeTest(new TextSpan(14, 1), 1, 8, namedObject),
            new IrInstruction.IsNull(new TextSpan(15, 1), 1, 2),
            new IrInstruction.HasProperty(new TextSpan(16, 1), 1, 6, 4),
            new IrInstruction.CreateArray(new TextSpan(17, 1), 5, mutableArray, [ 0 ]),
            new IrInstruction.CreateObject(
                new TextSpan(18, 1),
                6,
                namedObject,
                [ new IrInstruction.ObjectPropertyValue("value", 0) ]
            ),
            new IrInstruction.GetProperty(new TextSpan(19, 1), 0, 6, "value", false, false),
            new IrInstruction.SetProperty(new TextSpan(20, 1), 6, "value", 0),
            new IrInstruction.RemoveProperty(new TextSpan(21, 1), 6, "value"),
            new IrInstruction.GetElement(new TextSpan(22, 1), 0, 5, 0, false, true),
            new IrInstruction.SetElement(new TextSpan(23, 1), 5, 0, 0, false),
            new IrInstruction.RemoveElementProperty(new TextSpan(24, 1), 6, 4),
            new IrInstruction.ProviderCall(new TextSpan(25, 1), 0, "provider.call", TypeSymbols.Int, [ 0 ]),
            new IrInstruction.UserCall(new TextSpan(26, 1), null, "user.call", TypeSymbols.Void, [ 0 ]),
        ];
        IrFunction entry = new (
            "$entry",
            TypeSymbols.Int,
            0,
            slots,
            [
                new IrBasicBlock(
                    0,
                    instructions,
                    new IrTerminator.Branch(new TextSpan(27, 1), 1, 1, 2)
                ),
                new IrBasicBlock(
                    1,
                    [ ],
                    new IrTerminator.Jump(new TextSpan(28, 1), 2)
                ),
                new IrBasicBlock(
                    2,
                    [ ],
                    new IrTerminator.Return(new TextSpan(29, 1), 0)
                ),
            ]
        );
        IrFunction user = new (
            "user.call",
            TypeSymbols.Void,
            0,
            [ new IrSlot(0, IrSlotKind.Parameter, TypeSymbols.Int, "value") ],
            [
                new IrBasicBlock(
                    0,
                    [ ],
                    new IrTerminator.Return(new TextSpan(30, 0), null)
                ),
            ]
        );

        return new IrProgram(
            new EnvironmentFingerprint("env"),
            CompilationMode.Program,
            new LanguageProfileFingerprint("profile"),
            entry,
            [ user ]
        );
    }

    private static IrProgram CreateMinimalProgram()
    {
        return CreateMinimalProgram(TypeSymbols.Int, 42L);
    }

    private static IrProgram CreateMinimalProgram(
        TypeSymbol type,
        object? value
    )
    {
        IrFunction entry = new (
            "$entry",
            type,
            0,
            [ new IrSlot(0, IrSlotKind.Temporary, type, null) ],
            [
                new IrBasicBlock(
                    0,
                    [
                        new IrInstruction.Constant(
                            new TextSpan(0, 1),
                            0,
                            type,
                            value
                        ),
                    ],
                    new IrTerminator.Return(new TextSpan(0, 1), 0)
                ),
            ]
        );

        return new IrProgram(
            new EnvironmentFingerprint("env"),
            CompilationMode.Expression,
            new LanguageProfileFingerprint("profile"),
            entry,
            [ ]
        );
    }

    private static IrProgram CreateInstructionProgram(IrInstruction instruction)
    {
        IrFunction entry = new (
            "$entry",
            TypeSymbols.Int,
            0,
            [ new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null) ],
            [
                new IrBasicBlock(
                    0,
                    [ instruction ],
                    new IrTerminator.Return(default, 0)
                ),
            ]
        );

        return new IrProgram(
            new EnvironmentFingerprint("env"),
            CompilationMode.Expression,
            new LanguageProfileFingerprint("profile"),
            entry,
            [ ]
        );
    }

    private static IrProgram CreateTypeOnlyProgram(
        IReadOnlyList<TypeSymbol> slotTypes
    )
    {
        IrFunction entry = new (
            "$entry",
            TypeSymbols.Void,
            0,
            [
                .. slotTypes.Select(
                    static (type, id) => new IrSlot(
                        id,
                        IrSlotKind.Temporary,
                        type,
                        null
                    )
                ),
            ],
            [
                new IrBasicBlock(
                    0,
                    [ ],
                    new IrTerminator.Return(default, null)
                ),
            ]
        );

        return new IrProgram(
            new EnvironmentFingerprint("env"),
            CompilationMode.Program,
            new LanguageProfileFingerprint("profile"),
            entry,
            [ ]
        );
    }

    private static ObjectTypeSymbol CreateRecursiveCycle(int length)
    {
        ObjectTypeGraphBuilder builder = new ();
        IReadOnlyList<ObjectTypeGraphReference> nodes =
        [
            .. Enumerable.Range(0, length)
                .Select(index => builder.DeclareAnonymous($"node{index}", false)),
        ];

        for (int index = 0; index < nodes.Count; index++)
        {
            builder.AddProperty(
                nodes[index],
                "next",
                nodes[(index + 1) % nodes.Count]
            );
        }

        return (ObjectTypeSymbol)builder.Build()[nodes[0]];
    }
}
