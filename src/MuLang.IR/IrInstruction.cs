using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Represents an executable operation in a MuLang IR basic block.</summary>
/// <param name="Span">The source span associated with the operation.</param>
public abstract record IrInstruction(TextSpan Span)
{
    /// <summary>Loads a constant value into a slot.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Type">The constant type.</param>
    /// <param name="Value">The constant value.</param>
    public sealed record Constant(
        TextSpan Span,
        int Destination,
        TypeSymbol Type,
        object? Value
    ) : IrInstruction(Span);

    /// <summary>Copies a value between slots.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Source">The source slot.</param>
    public sealed record Copy(
        TextSpan Span,
        int Destination,
        int Source
    ) : IrInstruction(Span);

    /// <summary>Loads a provider global into a slot.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="GlobalId">The provider global identifier.</param>
    public sealed record LoadGlobal(
        TextSpan Span,
        int Destination,
        string GlobalId
    ) : IrInstruction(Span);

    /// <summary>Applies a unary operation.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Operator">The unary operator.</param>
    /// <param name="Operand">The operand slot.</param>
    public sealed record Unary(
        TextSpan Span,
        int Destination,
        IrUnaryOperator Operator,
        int Operand
    ) : IrInstruction(Span);

    /// <summary>Applies a binary operation.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Operator">The binary operator.</param>
    /// <param name="Left">The left operand slot.</param>
    /// <param name="Right">The right operand slot.</param>
    public sealed record Binary(
        TextSpan Span,
        int Destination,
        IrBinaryOperator Operator,
        int Left,
        int Right
    ) : IrInstruction(Span);

    /// <summary>Converts a value to another type.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Source">The source slot.</param>
    /// <param name="TargetType">The target type.</param>
    /// <param name="IsChecked">Whether overflow checking is required.</param>
    public sealed record Convert(
        TextSpan Span,
        int Destination,
        int Source,
        TypeSymbol TargetType,
        bool IsChecked
    ) : IrInstruction(Span);

    /// <summary>Converts a value to its truthiness result.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Source">The source slot.</param>
    public sealed record Truthiness(
        TextSpan Span,
        int Destination,
        int Source
    ) : IrInstruction(Span);

    /// <summary>Tests whether a value conforms to a type.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Source">The source slot.</param>
    /// <param name="TestedType">The tested type.</param>
    public sealed record TypeTest(
        TextSpan Span,
        int Destination,
        int Source,
        TypeSymbol TestedType
    ) : IrInstruction(Span);

    /// <summary>Tests whether a value is null.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Source">The source slot.</param>
    public sealed record IsNull(
        TextSpan Span,
        int Destination,
        int Source
    ) : IrInstruction(Span);

    /// <summary>Tests whether an object contains a property.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Target">The object slot.</param>
    /// <param name="Key">The property-name slot.</param>
    public sealed record HasProperty(
        TextSpan Span,
        int Destination,
        int Target,
        int Key
    ) : IrInstruction(Span);

    /// <summary>Creates an array value.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Type">The array type.</param>
    /// <param name="Elements">The element slots.</param>
    public sealed record CreateArray(
        TextSpan Span,
        int Destination,
        ArrayTypeSymbol Type,
        IReadOnlyCollection<int> Elements
    ) : IrInstruction(Span);

    /// <summary>Associates an object property name with its value slot.</summary>
    /// <param name="Name">The property name.</param>
    /// <param name="Value">The value slot.</param>
    public sealed record ObjectPropertyValue(string Name, int Value);

    /// <summary>Creates an object value.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Type">The object type.</param>
    /// <param name="Properties">The object properties.</param>
    public sealed record CreateObject(
        TextSpan Span,
        int Destination,
        ObjectTypeSymbol Type,
        IReadOnlyCollection<ObjectPropertyValue> Properties
    ) : IrInstruction(Span);

    /// <summary>Reads a named property.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Target">The target slot.</param>
    /// <param name="Name">The property name.</param>
    /// <param name="IsOptional">Whether null propagates through the access.</param>
    /// <param name="IsArrayLength">Whether the access represents an array length operation.</param>
    public sealed record GetProperty(
        TextSpan Span,
        int Destination,
        int Target,
        string Name,
        bool IsOptional,
        bool IsArrayLength
    ) : IrInstruction(Span);

    /// <summary>Writes a named property.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Target">The target slot.</param>
    /// <param name="Name">The property name.</param>
    /// <param name="Value">The value slot.</param>
    public sealed record SetProperty(
        TextSpan Span,
        int Target,
        string Name,
        int Value
    ) : IrInstruction(Span);

    /// <summary>Removes a named property.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Target">The target slot.</param>
    /// <param name="Name">The property name.</param>
    public sealed record RemoveProperty(
        TextSpan Span,
        int Target,
        string Name
    ) : IrInstruction(Span);

    /// <summary>Reads an indexed element or dynamic object property.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot.</param>
    /// <param name="Target">The target slot.</param>
    /// <param name="Index">The index or key slot.</param>
    /// <param name="IsObjectAccess">Whether the target is an object.</param>
    /// <param name="IsOptional">Whether null propagates through the access.</param>
    public sealed record GetElement(
        TextSpan Span,
        int Destination,
        int Target,
        int Index,
        bool IsObjectAccess,
        bool IsOptional
    ) : IrInstruction(Span);

    /// <summary>Writes an indexed element or dynamic object property.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Target">The target slot.</param>
    /// <param name="Index">The index or key slot.</param>
    /// <param name="Value">The value slot.</param>
    /// <param name="IsObjectAccess">Whether the target is an object.</param>
    public sealed record SetElement(
        TextSpan Span,
        int Target,
        int Index,
        int Value,
        bool IsObjectAccess
    ) : IrInstruction(Span);

    /// <summary>Removes a dynamically named object property.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Target">The target slot.</param>
    /// <param name="Key">The property-name slot.</param>
    public sealed record RemoveElementProperty(
        TextSpan Span,
        int Target,
        int Key
    ) : IrInstruction(Span);

    /// <summary>Invokes a provider function.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot, or <c>null</c> for a void call.</param>
    /// <param name="FunctionId">The provider function identifier.</param>
    /// <param name="ReturnType">The declared return type.</param>
    /// <param name="Arguments">The argument slots.</param>
    public sealed record ProviderCall(
        TextSpan Span,
        int? Destination,
        string FunctionId,
        TypeSymbol ReturnType,
        IReadOnlyList<int> Arguments
    ) : IrInstruction(Span);

    /// <summary>Invokes a user-defined function.</summary>
    /// <param name="Span">The source span.</param>
    /// <param name="Destination">The destination slot, or <c>null</c> for a void call.</param>
    /// <param name="FunctionId">The user-function identifier.</param>
    /// <param name="ReturnType">The declared return type.</param>
    /// <param name="Arguments">The argument slots.</param>
    public sealed record UserCall(
        TextSpan Span,
        int? Destination,
        string FunctionId,
        TypeSymbol ReturnType,
        IReadOnlyList<int> Arguments
    ) : IrInstruction(Span);
}
