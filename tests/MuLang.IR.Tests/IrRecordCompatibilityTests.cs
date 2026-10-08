using MuLang.Core.Types;
using System.Reflection;

namespace MuLang.IR.Tests;

public sealed class IrRecordCompatibilityTests
{
    [Test]
    public void LegacyConstructorsAndDeconstructorsAreObsolete()
    {
        AssertObsoleteMembers<IrSlot>([ 4, 5 ], [ 4 ]);
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
            "value",
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
            string? slotName,
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
            Assert.That(slotName, Is.EqualTo("value"));
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
