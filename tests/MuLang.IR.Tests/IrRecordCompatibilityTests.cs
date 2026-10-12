using MuLang.Core.Types;
using System.Reflection;

namespace MuLang.IR.Tests;

public sealed class IrRecordCompatibilityTests
{
    [Test]
    public void LegacyConstructorsAndDeconstructorsAreObsolete()
    {
        Assert.That(
            typeof(IrSlot).GetConstructors()
                .Where(
                    static constructor =>
                        constructor.GetParameters().Any(
                            static parameter => parameter.Name is "Name" or "name"
                        )
                ),
            Has.All.Matches<ConstructorInfo>(
                static constructor =>
                    constructor.IsDefined(typeof(ObsoleteAttribute), false)
            )
        );
        Assert.That(
            typeof(IrSlot).GetConstructors()
                .Single(
                    static constructor =>
                        constructor.GetParameters().Length == 5 &&
                        constructor.GetParameters()[3].ParameterType ==
                        typeof(IrSlotMutability)
                )
                .IsDefined(typeof(ObsoleteAttribute), false),
            Is.False
        );
        Assert.That(
            typeof(IrSlot).GetMethods()
                .Where(
                    static method =>
                        method.Name == "Deconstruct" &&
                        method.GetParameters().Any(
                            static parameter => parameter.Name == "Name"
                        )
                ),
            Has.All.Matches<MethodInfo>(
                static method =>
                    method.IsDefined(typeof(ObsoleteAttribute), false)
            )
        );
        AssertObsoleteMembers<IrBasicBlock>([ 3 ], [ 3 ]);
        AssertObsoleteMembers<IrFunction>([ 5, 6 ], [ 5 ]);
    }

    [Test]
    public void LegacyConstructorsPreserveImplicitState()
    {
#pragma warning disable CS0618
        IrSlot slot = new (
            0,
            IrSlotKind.Parameter,
            TypeSymbols.Int,
            "value"
        );
        IrBasicBlock block = new (
            0,
            [ ],
            new IrTerminator.Return(default, null)
        );
        IrFunction function = new (
            "$entry",
            TypeSymbols.Void,
            0,
            [ slot ],
            [ block ]
        );
#pragma warning restore CS0618

        using (Assert.EnterMultipleScope())
        {
            Assert.That(slot.Mutability, Is.EqualTo(IrSlotMutability.ReadOnly));
            Assert.That(slot.LifetimeRegion, Is.Zero);
#pragma warning disable CS0618
            Assert.That(slot.Name, Is.Null);
#pragma warning restore CS0618
            Assert.That(block.LifetimeRegion, Is.Zero);
            Assert.That(
                function.LifetimeRegions,
                Is.EqualTo([ new IrLifetimeRegion(0, null, 0) ])
            );
            Assert.That(function.ExceptionRegions, Is.Empty);
        }
    }

    [Test]
    public void PrimaryDeconstructorsExposeCompleteState()
    {
        IrSlot slot = new (
            0,
            IrSlotKind.Local,
            TypeSymbols.Int,
            IrSlotMutability.ReadOnly,
            1
        );
        IrBasicBlock block = new (
            2,
            [ ],
            new IrTerminator.Return(default, null),
            1
        );
        IReadOnlyList<IrLifetimeRegion> lifetimeRegions =
        [
            new (0, null, 0),
            new (1, 0, 2),
        ];
        IReadOnlyList<IrExceptionRegion> exceptionRegions = [ ];
        IrFunction function = new (
            "$entry",
            TypeSymbols.Void,
            0,
            [ slot ],
            [ block ],
            lifetimeRegions,
            exceptionRegions
        );

        (
            int slotId,
            IrSlotKind slotKind,
            TypeSymbol slotType,
            IrSlotMutability slotMutability,
            int slotLifetimeRegion
        ) = slot;
        (
            int blockId,
            IReadOnlyCollection<IrInstruction> instructions,
            IrTerminator terminator,
            int blockLifetimeRegion
        ) = block;
        (
            string functionId,
            TypeSymbol returnType,
            int entryBlock,
            IReadOnlyList<IrSlot> slots,
            IReadOnlyList<IrBasicBlock> blocks,
            IReadOnlyList<IrLifetimeRegion> deconstructedLifetimeRegions,
            IReadOnlyList<IrExceptionRegion> deconstructedExceptionRegions
        ) = function;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(slotId, Is.Zero);
            Assert.That(slotKind, Is.EqualTo(IrSlotKind.Local));
            Assert.That(slotType, Is.SameAs(TypeSymbols.Int));
            Assert.That(slotMutability, Is.EqualTo(IrSlotMutability.ReadOnly));
            Assert.That(slotLifetimeRegion, Is.EqualTo(1));
            Assert.That(blockId, Is.EqualTo(2));
            Assert.That(instructions, Is.Empty);
            Assert.That(terminator, Is.TypeOf<IrTerminator.Return>());
            Assert.That(blockLifetimeRegion, Is.EqualTo(1));
            Assert.That(functionId, Is.EqualTo("$entry"));
            Assert.That(returnType, Is.SameAs(TypeSymbols.Void));
            Assert.That(entryBlock, Is.Zero);
            Assert.That(slots, Is.EqualTo([ slot ]));
            Assert.That(blocks, Is.EqualTo([ block ]));
            Assert.That(deconstructedLifetimeRegions, Is.SameAs(lifetimeRegions));
            Assert.That(deconstructedExceptionRegions, Is.SameAs(exceptionRegions));
        }
    }

    private static void AssertObsoleteMembers<T>(
        IReadOnlyCollection<int> obsoleteConstructorParameterCounts,
        IReadOnlyCollection<int> obsoleteDeconstructorParameterCounts
    )
    {
        foreach (ConstructorInfo constructor in typeof(T).GetConstructors())
        {
            bool expected = obsoleteConstructorParameterCounts.Contains(
                constructor.GetParameters().Length
            );
            Assert.That(
                constructor.IsDefined(typeof(ObsoleteAttribute), false),
                Is.EqualTo(expected)
            );
        }

        foreach (
            MethodInfo deconstructor in typeof(T)
                .GetMethods()
                .Where(static method => method.Name == "Deconstruct")
        )
        {
            bool expected = obsoleteDeconstructorParameterCounts.Contains(
                deconstructor.GetParameters().Length
            );
            Assert.That(
                deconstructor.IsDefined(typeof(ObsoleteAttribute), false),
                Is.EqualTo(expected)
            );
        }
    }
}
