using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.IR;

internal abstract record IrInstruction(TextSpan Span)
{
    internal sealed record Constant(
        TextSpan Span,
        int Destination,
        TypeSymbol Type,
        object? Value
    ) : IrInstruction(Span);

    internal sealed record Copy(
        TextSpan Span,
        int Destination,
        int Source
    ) : IrInstruction(Span);

    internal sealed record LoadGlobal(
        TextSpan Span,
        int Destination,
        string GlobalId
    ) : IrInstruction(Span);

    internal sealed record Unary(
        TextSpan Span,
        int Destination,
        IrUnaryOperator Operator,
        int Operand
    ) : IrInstruction(Span);

    internal sealed record Binary(
        TextSpan Span,
        int Destination,
        IrBinaryOperator Operator,
        int Left,
        int Right
    ) : IrInstruction(Span);

    internal sealed record Convert(
        TextSpan Span,
        int Destination,
        int Source,
        TypeSymbol TargetType,
        bool IsChecked
    ) : IrInstruction(Span);

    internal sealed record TypeTest(
        TextSpan Span,
        int Destination,
        int Source,
        TypeSymbol TestedType
    ) : IrInstruction(Span);

    internal sealed record IsNull(
        TextSpan Span,
        int Destination,
        int Source
    ) : IrInstruction(Span);

    internal sealed record HasProperty(
        TextSpan Span,
        int Destination,
        int Target,
        int Key
    ) : IrInstruction(Span);

    internal sealed record CreateArray(
        TextSpan Span,
        int Destination,
        ArrayTypeSymbol Type,
        IReadOnlyCollection<int> Elements
    ) : IrInstruction(Span);

    internal sealed record ObjectPropertyValue(string Name, int Value);

    internal sealed record CreateObject(
        TextSpan Span,
        int Destination,
        ObjectTypeSymbol Type,
        IReadOnlyCollection<ObjectPropertyValue> Properties
    ) : IrInstruction(Span);

    internal sealed record GetProperty(
        TextSpan Span,
        int Destination,
        int Target,
        string Name,
        bool IsOptional,
        bool IsArrayLength
    ) : IrInstruction(Span);

    internal sealed record SetProperty(
        TextSpan Span,
        int Target,
        string Name,
        int Value
    ) : IrInstruction(Span);

    internal sealed record RemoveProperty(
        TextSpan Span,
        int Target,
        string Name
    ) : IrInstruction(Span);

    internal sealed record GetElement(
        TextSpan Span,
        int Destination,
        int Target,
        int Index,
        bool IsObjectAccess,
        bool IsOptional
    ) : IrInstruction(Span);

    internal sealed record SetElement(
        TextSpan Span,
        int Target,
        int Index,
        int Value,
        bool IsObjectAccess
    ) : IrInstruction(Span);

    internal sealed record RemoveElementProperty(
        TextSpan Span,
        int Target,
        int Key
    ) : IrInstruction(Span);

    internal sealed record ProviderCall(
        TextSpan Span,
        int? Destination,
        string FunctionId,
        TypeSymbol ReturnType,
        IReadOnlyList<int> Arguments
    ) : IrInstruction(Span);

    internal sealed record UserCall(
        TextSpan Span,
        int? Destination,
        string FunctionId,
        TypeSymbol ReturnType,
        IReadOnlyList<int> Arguments
    ) : IrInstruction(Span);
}
