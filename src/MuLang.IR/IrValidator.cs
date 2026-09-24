using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Collections.ObjectModel;

namespace MuLang.IR;

/// <summary>Validates portable MuLang IR against its structural and environment contracts.</summary>
public static class IrValidator
{
    /// <summary>Validates a program against an environment.</summary>
    /// <param name="program">The IR program to validate.</param>
    /// <param name="environment">The expected provider environment.</param>
    /// <returns>The validation diagnostics.</returns>
    public static DiagnosticCollection Validate(
        IrProgram program,
        EnvironmentSchema environment
    )
    {
        return Validate(program, environment, null, null);
    }

    /// <summary>Validates a program against an environment and expected compilation metadata.</summary>
    /// <param name="program">The IR program to validate.</param>
    /// <param name="environment">The expected provider environment.</param>
    /// <param name="expectedCompilationMode">The expected compilation mode, or <c>null</c> to skip this check.</param>
    /// <param name="expectedLanguageProfileFingerprint">The expected profile fingerprint, or <c>null</c> to skip this check.</param>
    /// <returns>The validation diagnostics.</returns>
    public static DiagnosticCollection Validate(
        IrProgram program,
        EnvironmentSchema environment,
        CompilationMode? expectedCompilationMode,
        LanguageProfileFingerprint? expectedLanguageProfileFingerprint
    )
    {
        if (program is null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        ICollection<Diagnostic> diagnostics = [ ];

        if (!Enum.IsDefined(program.CompilationMode))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.CompilationMetadataMismatch,
                default,
                "The IR compilation mode is invalid."
            );
        }

        if (program.EnvironmentFingerprint != environment.Fingerprint)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.EnvironmentMismatch,
                default,
                "The IR environment fingerprint does not match the supplied environment."
            );
        }

        if (
            expectedCompilationMode is not null &&
            program.CompilationMode != expectedCompilationMode
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.CompilationMetadataMismatch,
                default,
                "The IR compilation mode does not match the expected compilation mode."
            );
        }

        if (
            expectedLanguageProfileFingerprint is not null &&
            program.LanguageProfileFingerprint != expectedLanguageProfileFingerprint
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.CompilationMetadataMismatch,
                default,
                "The IR language profile fingerprint does not match the expected language profile."
            );
        }

        ISet<string> functionIds = new HashSet<string>(StringComparer.Ordinal)
        {
            program.EntryFunction.Id,
        };

        foreach (IrFunction function in program.UserFunctions)
        {
            if (!functionIds.Add(function.Id))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    default,
                    $"IR function '{function.Id}' is declared more than once."
                );
            }
        }

        ValidateFunction(program, program.EntryFunction, environment, diagnostics);

        foreach (IrFunction function in program.UserFunctions)
        {
            ValidateFunction(program, function, environment, diagnostics);
        }

        return DiagnosticCollection.Create(diagnostics);
    }

    private static void ValidateFunction(
        IrProgram program,
        IrFunction function,
        EnvironmentSchema environment,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrProgram functionProgram = new (
            program.EnvironmentFingerprint,
            program.CompilationMode,
            program.LanguageProfileFingerprint,
            function,
            program.UserFunctions
        );
        ValidateSlots(functionProgram, diagnostics);
        IReadOnlyDictionary<int, IrBasicBlock> blocksById = ValidateBlocks(
            functionProgram,
            diagnostics
        );

        if (!blocksById.ContainsKey(functionProgram.EntryBlock))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidStructure,
                default,
                $"Entry block {functionProgram.EntryBlock} does not exist in function '{function.Id}'."
            );
        }

        ValidateParameterSlots(
            function,
            ReferenceEquals(function, program.EntryFunction),
            diagnostics
        );

        foreach (IrBasicBlock block in functionProgram.Blocks)
        {
            ValidateBlock(functionProgram, environment, blocksById, block, diagnostics);
        }

        ValidateDefinitions(functionProgram, blocksById, diagnostics);
    }

    private static void ValidateParameterSlots(
        IrFunction function,
        bool isEntryFunction,
        ICollection<Diagnostic> diagnostics
    )
    {
        bool hasNonParameter = false;
        int parameterCount = 0;

        foreach (IrSlot slot in function.Slots)
        {
            if (slot.Kind == IrSlotKind.Parameter)
            {
                parameterCount++;

                if (hasNonParameter)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidSlot,
                        default,
                        $"Parameter slot {slot.Id} in function '{function.Id}' is not contiguous."
                    );
                }
            }
            else
            {
                hasNonParameter = true;
            }
        }

        if (isEntryFunction && parameterCount != 0)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidSlot,
                default,
                "The IR entry function cannot declare parameter slots."
            );
        }
    }

    private static void ValidateSlots(
        IrProgram program,
        ICollection<Diagnostic> diagnostics
    )
    {
        for (int index = 0; index < program.Slots.Count; index++)
        {
            IrSlot slot = program.Slots[index];

            if (slot.Id != index)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR slot at index {index} has identifier {slot.Id}."
                );
            }

            if (slot.Type.Kind is TypeKind.Void or TypeKind.Null or TypeKind.Error)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidSlot,
                    default,
                    $"IR slot {slot.Id} has invalid type '{slot.Type.DisplayName}'."
                );
            }
        }
    }

    private static Dictionary<int, IrBasicBlock> ValidateBlocks(
        IrProgram program,
        ICollection<Diagnostic> diagnostics
    )
    {
        Dictionary<int, IrBasicBlock> blocksById = [ ];

        for (int index = 0; index < program.Blocks.Count; index++)
        {
            IrBasicBlock block = program.Blocks[index];

            if (block.Id != index)
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    block.Terminator.Span,
                    $"IR block at index {index} has identifier {block.Id}."
                );
            }

            if (!blocksById.TryAdd(block.Id, block))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.InvalidStructure,
                    block.Terminator.Span,
                    $"IR block {block.Id} is declared more than once."
                );
            }
        }

        return blocksById;
    }

    private static void ValidateBlock(
        IrProgram program,
        EnvironmentSchema environment,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        IrBasicBlock block,
        ICollection<Diagnostic> diagnostics
    )
    {
        foreach (IrInstruction instruction in block.Instructions)
        {
            ValidateInstruction(program, environment, instruction, diagnostics);
        }

        switch (block.Terminator)
        {
            case IrTerminator.Jump jump:
            {
                ValidateBlockTarget(blocksById, jump.TargetBlock, jump.Span, diagnostics);
                break;
            }

            case IrTerminator.Branch branch:
            {
                ValidateSlotType(
                    program,
                    branch.Condition,
                    TypeSymbols.Bool,
                    branch.Span,
                    diagnostics
                );
                ValidateBlockTarget(blocksById, branch.TrueBlock, branch.Span, diagnostics);
                ValidateBlockTarget(blocksById, branch.FalseBlock, branch.Span, diagnostics);
                break;
            }

            case IrTerminator.Return result:
            {
                if (program.ResultType.Kind == TypeKind.Void)
                {
                    if (result.Value is not null)
                    {
                        Report(
                            diagnostics,
                            IrDiagnosticCodes.TypeMismatch,
                            result.Span,
                            "A void IR program cannot return a value."
                        );
                    }
                }
                else if (result.Value is null)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        result.Span,
                        $"IR program must return '{program.ResultType.DisplayName}'."
                    );
                }
                else
                {
                    ValidateSlotType(
                        program,
                        result.Value.Value,
                        program.ResultType,
                        result.Span,
                        diagnostics
                    );
                }

                break;
            }
        }
    }

    private static void ValidateInstruction(
        IrProgram program,
        EnvironmentSchema environment,
        IrInstruction instruction,
        ICollection<Diagnostic> diagnostics
    )
    {
        switch (instruction)
        {
            case IrInstruction.Constant constant:
            {
                ValidateSlotType(
                    program,
                    constant.Destination,
                    constant.Type,
                    constant.Span,
                    diagnostics
                );

                if (!IsConstantCompatible(constant.Value, constant.Type))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        constant.Span,
                        $"IR constant is incompatible with '{constant.Type.DisplayName}'."
                    );
                }

                break;
            }

            case IrInstruction.Copy copy:
            {
                ValidateEquivalentSlots(
                    program,
                    copy.Destination,
                    copy.Source,
                    copy.Span,
                    diagnostics
                );
                break;
            }

            case IrInstruction.LoadGlobal global:
            {
                if (!environment.TryGetGlobalById(global.GlobalId, out GlobalSymbol? symbol))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.UndefinedProviderSymbol,
                        global.Span,
                        $"Global '{global.GlobalId}' is not declared."
                    );
                    break;
                }

                ValidateSlotType(
                    program,
                    global.Destination,
                    symbol.Type,
                    global.Span,
                    diagnostics
                );
                break;
            }

            case IrInstruction.Unary unary:
            {
                ValidateUnary(program, unary, diagnostics);
                break;
            }

            case IrInstruction.Binary binary:
            {
                ValidateBinary(program, binary, diagnostics);
                break;
            }

            case IrInstruction.Convert conversion:
            {
                ValidateSlotType(
                    program,
                    conversion.Destination,
                    conversion.TargetType,
                    conversion.Span,
                    diagnostics
                );
                IrSlot? sourceSlot = GetSlot(program, conversion.Source);

                if (sourceSlot is null)
                {
                    ValidateSlot(program, conversion.Source, conversion.Span, diagnostics);
                    break;
                }

                ConversionKind conversionKind = TypeRelations.ClassifyConversion(
                    sourceSlot.Type,
                    conversion.TargetType
                );
                bool isValid = conversion.IsCast
                    ? conversion.IsChecked &&
                        TypeRelations.IsCastable(
                            sourceSlot.Type,
                            conversion.TargetType
                        )
                    : conversionKind != ConversionKind.None &&
                        (
                            !RequiresCheckedArrayCapabilityAcquisition(
                                sourceSlot.Type,
                                conversion.TargetType
                            ) ||
                            conversion.IsChecked
                        );

                if (!isValid)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        conversion.Span,
                        $"IR conversion from '{sourceSlot.Type.DisplayName}' to '{conversion.TargetType.DisplayName}' is invalid."
                    );
                }

                break;
            }

            case IrInstruction.Truthiness truthiness:
            {
                ValidateSlotType(
                    program,
                    truthiness.Destination,
                    TypeSymbols.Bool,
                    truthiness.Span,
                    diagnostics
                );
                IrSlot? source = GetSlot(program, truthiness.Source);

                if (source is null)
                {
                    ValidateSlot(
                        program,
                        truthiness.Source,
                        truthiness.Span,
                        diagnostics
                    );
                    break;
                }

                if (source.Type.Kind == TypeKind.Void)
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.TypeMismatch,
                        truthiness.Span,
                        "IR truthiness source cannot have type 'void'."
                    );
                }

                break;
            }

            case IrInstruction.TypeTest typeTest:
            {
                ValidateSlotType(
                    program,
                    typeTest.Destination,
                    TypeSymbols.Bool,
                    typeTest.Span,
                    diagnostics
                );
                ValidateSlot(program, typeTest.Source, typeTest.Span, diagnostics);
                break;
            }

            case IrInstruction.IsNull isNull:
            {
                ValidateSlotType(
                    program,
                    isNull.Destination,
                    TypeSymbols.Bool,
                    isNull.Span,
                    diagnostics
                );
                ValidateSlot(program, isNull.Source, isNull.Span, diagnostics);
                break;
            }

            case IrInstruction.HasProperty propertyTest:
            {
                ValidateSlotType(
                    program,
                    propertyTest.Destination,
                    TypeSymbols.Bool,
                    propertyTest.Span,
                    diagnostics
                );
                ValidateSlot(program, propertyTest.Target, propertyTest.Span, diagnostics);
                ValidateSlotType(
                    program,
                    propertyTest.Key,
                    TypeSymbols.String,
                    propertyTest.Span,
                    diagnostics
                );
                break;
            }

            case IrInstruction.CreateArray array:
            {
                ValidateSlotType(
                    program,
                    array.Destination,
                    array.Type,
                    array.Span,
                    diagnostics
                );

                foreach (int element in array.Elements)
                {
                    ValidateSlotType(
                        program,
                        element,
                        array.Type.ElementType,
                        array.Span,
                        diagnostics
                    );
                }

                break;
            }

            case IrInstruction.CreateObject objectValue:
            {
                ValidateSlotType(
                    program,
                    objectValue.Destination,
                    objectValue.Type,
                    objectValue.Span,
                    diagnostics
                );

                foreach (IrInstruction.ObjectPropertyValue property in objectValue.Properties)
                {
                    ValidateSlot(program, property.Value, objectValue.Span, diagnostics);
                }

                if (
                    objectValue.Properties
                        .Select(static property => property.Name)
                        .Distinct(StringComparer.Ordinal)
                        .Count() != objectValue.Properties.Count
                )
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.InvalidStructure,
                        objectValue.Span,
                        "IR object creation contains duplicate property names."
                    );
                }

                break;
            }

            case IrInstruction.GetProperty property:
            {
                ValidatePropertyRead(program, property, diagnostics);
                break;
            }

            case IrInstruction.SetProperty property:
            {
                ValidatePropertyWrite(program, property, diagnostics);
                break;
            }

            case IrInstruction.RemoveProperty property:
            {
                ValidateSlot(program, property.Target, property.Span, diagnostics);
                break;
            }

            case IrInstruction.GetElement element:
            {
                ValidateElementRead(program, element, diagnostics);
                break;
            }

            case IrInstruction.SetElement element:
            {
                ValidateElementWrite(program, element, diagnostics);
                break;
            }

            case IrInstruction.RemoveElementProperty property:
            {
                ValidateSlot(program, property.Target, property.Span, diagnostics);
                ValidateSlotType(
                    program,
                    property.Key,
                    TypeSymbols.String,
                    property.Span,
                    diagnostics
                );
                break;
            }

            case IrInstruction.ProviderCall call:
            {
                ValidateProviderCall(program, environment, call, diagnostics);
                break;
            }

            case IrInstruction.UserCall call:
            {
                ValidateUserCall(program, call, diagnostics);
                break;
            }
        }
    }

    private static void ValidateUnary(
        IrProgram program,
        IrInstruction.Unary unary,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, unary.Destination);
        IrSlot? operand = GetSlot(program, unary.Operand);

        if (destination is null || operand is null)
        {
            ValidateSlot(program, unary.Destination, unary.Span, diagnostics);
            ValidateSlot(program, unary.Operand, unary.Span, diagnostics);
            return;
        }

        TypeSymbol? expectedType = unary.Operator switch
        {
            IrUnaryOperator.Identity or IrUnaryOperator.Negate
                when destination.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number =>
                destination.Type,
            IrUnaryOperator.LogicalNot => TypeSymbols.Bool,
            IrUnaryOperator.BitwiseNot => TypeSymbols.Int,
            _ => null,
        };

        if (
            expectedType is null ||
            !TypeRelations.AreEquivalent(destination.Type, expectedType) ||
            !TypeRelations.AreEquivalent(operand.Type, expectedType)
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                unary.Span,
                "IR unary instruction has incompatible operand or destination types."
            );
        }
    }

    private static void ValidateBinary(
        IrProgram program,
        IrInstruction.Binary binary,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, binary.Destination);
        IrSlot? left = GetSlot(program, binary.Left);
        IrSlot? right = GetSlot(program, binary.Right);

        if (destination is null || left is null || right is null)
        {
            ValidateSlot(program, binary.Destination, binary.Span, diagnostics);
            ValidateSlot(program, binary.Left, binary.Span, diagnostics);
            ValidateSlot(program, binary.Right, binary.Span, diagnostics);
            return;
        }

        bool isValid = binary.Operator switch
        {
            IrBinaryOperator.Add =>
                AreEquivalent(left, right, destination) &&
                destination.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number or
                    TypeKind.String,
            IrBinaryOperator.Subtract or
                IrBinaryOperator.Multiply or
                IrBinaryOperator.Divide or
                IrBinaryOperator.Remainder =>
                AreEquivalent(left, right, destination) &&
                destination.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number,
            IrBinaryOperator.LeftShift or
                IrBinaryOperator.RightShift =>
                AreType(left, TypeSymbols.Int) &&
                AreType(right, TypeSymbols.Int) &&
                AreType(destination, TypeSymbols.Int),
            IrBinaryOperator.BitwiseAnd or
                IrBinaryOperator.BitwiseXor or
                IrBinaryOperator.BitwiseOr =>
                AreEquivalent(left, right, destination) &&
                destination.Type.Kind is TypeKind.Int or TypeKind.Bool,
            IrBinaryOperator.LessThan or
                IrBinaryOperator.LessThanOrEqual or
                IrBinaryOperator.GreaterThan or
                IrBinaryOperator.GreaterThanOrEqual =>
                AreType(destination, TypeSymbols.Bool) &&
                TypeRelations.AreEquivalent(left.Type, right.Type) &&
                left.Type.Kind is
                    TypeKind.Int or
                    TypeKind.Float or
                    TypeKind.Number or
                    TypeKind.String,
            IrBinaryOperator.StructuralEqual or
                IrBinaryOperator.StructuralNotEqual or
                IrBinaryOperator.IdentityEqual or
                IrBinaryOperator.IdentityNotEqual =>
                AreType(destination, TypeSymbols.Bool),
            _ => false,
        };

        if (!isValid)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                binary.Span,
                "IR binary instruction has incompatible operand or destination types."
            );
        }
    }

    private static void ValidatePropertyRead(
        IrProgram program,
        IrInstruction.GetProperty property,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, property.Destination);
        IrSlot? target = GetSlot(program, property.Target);

        if (destination is null || target is null)
        {
            ValidateSlot(program, property.Destination, property.Span, diagnostics);
            ValidateSlot(program, property.Target, property.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        TypeSymbol? expectedType = null;

        if (property.IsArrayLength && targetType is ArrayTypeSymbol)
        {
            expectedType =
                property.IsOptional && target.Type is NullableTypeSymbol
                    ? TypeSymbols.Nullable(TypeSymbols.Int)
                    : TypeSymbols.Int;
        }
        else if (targetType is ObjectTypeSymbol objectType)
        {
            if (objectType.TryGetProperty(property.Name, out ObjectPropertySymbol? symbol))
            {
                expectedType =
                    property.IsOptional &&
                    (target.Type is NullableTypeSymbol || symbol.IsOptional)
                        ? MakeNullable(symbol.Type)
                        : symbol.Type;
            }
            else if (objectType.IsOpen)
            {
                expectedType = TypeSymbols.Nullable(TypeSymbols.Unknown);
            }
        }
        else if (targetType.Kind == TypeKind.Object)
        {
            expectedType = TypeSymbols.Nullable(TypeSymbols.Unknown);
        }

        if (
            expectedType is null ||
            !TypeRelations.AreEquivalent(destination.Type, expectedType)
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                property.Span,
                "IR property read has incompatible target or destination types."
            );
        }
    }

    private static void ValidatePropertyWrite(
        IrProgram program,
        IrInstruction.SetProperty property,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? target = GetSlot(program, property.Target);
        IrSlot? value = GetSlot(program, property.Value);

        if (target is null || value is null)
        {
            ValidateSlot(program, property.Target, property.Span, diagnostics);
            ValidateSlot(program, property.Value, property.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        TypeSymbol? expectedType = null;

        if (
            targetType is ObjectTypeSymbol objectType &&
            objectType.TryGetProperty(property.Name, out ObjectPropertySymbol? symbol)
        )
        {
            expectedType = symbol.Type;
        }
        else if (
            targetType.Kind == TypeKind.Object ||
            targetType is ObjectTypeSymbol { IsOpen: true }
        )
        {
            expectedType = TypeSymbols.Nullable(TypeSymbols.Unknown);
        }

        if (
            expectedType is null ||
            !TypeRelations.AreEquivalent(value.Type, expectedType)
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                property.Span,
                "IR property write has incompatible target or value types."
            );
        }
    }

    private static void ValidateElementRead(
        IrProgram program,
        IrInstruction.GetElement element,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destination = GetSlot(program, element.Destination);
        IrSlot? target = GetSlot(program, element.Target);
        IrSlot? index = GetSlot(program, element.Index);

        if (destination is null || target is null || index is null)
        {
            ValidateSlot(program, element.Destination, element.Span, diagnostics);
            ValidateSlot(program, element.Target, element.Span, diagnostics);
            ValidateSlot(program, element.Index, element.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        bool isValid;

        if (element.IsObjectAccess)
        {
            isValid =
                targetType.Kind is TypeKind.Object or TypeKind.StructuredObject &&
                AreType(index, TypeSymbols.String);
        }
        else
        {
            isValid =
                targetType is ArrayTypeSymbol arrayType &&
                AreType(index, TypeSymbols.Int) &&
                TypeRelations.AreEquivalent(
                    destination.Type,
                    element.IsOptional && target.Type is NullableTypeSymbol
                        ? MakeNullable(arrayType.ElementType)
                        : arrayType.ElementType
                );
        }

        if (!isValid)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                element.Span,
                "IR element read has incompatible target, index, or destination types."
            );
        }
    }

    private static void ValidateElementWrite(
        IrProgram program,
        IrInstruction.SetElement element,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? target = GetSlot(program, element.Target);
        IrSlot? index = GetSlot(program, element.Index);
        IrSlot? value = GetSlot(program, element.Value);

        if (target is null || index is null || value is null)
        {
            ValidateSlot(program, element.Target, element.Span, diagnostics);
            ValidateSlot(program, element.Index, element.Span, diagnostics);
            ValidateSlot(program, element.Value, element.Span, diagnostics);
            return;
        }

        TypeSymbol targetType = GetNonNullable(target.Type);
        bool isValid = element.IsObjectAccess
            ? targetType.Kind is TypeKind.Object or TypeKind.StructuredObject &&
            AreType(index, TypeSymbols.String)
            : targetType is ArrayTypeSymbol { IsReadOnly: false } arrayType &&
            AreType(index, TypeSymbols.Int) &&
            TypeRelations.AreEquivalent(value.Type, arrayType.ElementType);

        if (!isValid)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                element.Span,
                "IR element write has incompatible target, index, or value types."
            );
        }
    }

    private static bool IsConstantCompatible(object? value, TypeSymbol type)
    {
        if (type is NullableTypeSymbol nullable)
        {
            return value is null || IsConstantCompatible(value, nullable.UnderlyingType);
        }

        return type.Kind switch
        {
            TypeKind.Bool => value is bool,
            TypeKind.Int => value is long,
            TypeKind.Float => value is double,
            TypeKind.Number => value is long or double,
            TypeKind.String => value is string,
            TypeKind.Unknown => value is not null,
            _ => false,
        };
    }

    private static bool RequiresCheckedArrayCapabilityAcquisition(
        TypeSymbol source,
        TypeSymbol target
    )
    {
        TypeSymbol nonNullableSource = GetNonNullable(source);
        TypeSymbol nonNullableTarget = GetNonNullable(target);

        return nonNullableSource is ArrayTypeSymbol { IsReadOnly: true } &&
            nonNullableTarget is ArrayTypeSymbol { IsReadOnly: false };
    }

    private static bool AreEquivalent(
        IrSlot first,
        IrSlot second,
        IrSlot third
    )
    {
        return TypeRelations.AreEquivalent(first.Type, second.Type) &&
            TypeRelations.AreEquivalent(first.Type, third.Type);
    }

    private static bool AreType(IrSlot slot, TypeSymbol type)
    {
        return TypeRelations.AreEquivalent(slot.Type, type);
    }

    private static TypeSymbol GetNonNullable(TypeSymbol type)
    {
        return type is NullableTypeSymbol nullable
            ? nullable.UnderlyingType
            : type;
    }

    private static TypeSymbol MakeNullable(TypeSymbol type)
    {
        return type is NullableTypeSymbol
            ? type
            : TypeSymbols.Nullable(type);
    }

    private static void ValidateProviderCall(
        IrProgram program,
        EnvironmentSchema environment,
        IrInstruction.ProviderCall call,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (!environment.TryGetFunctionById(call.FunctionId, out FunctionSymbol? function))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.UndefinedProviderSymbol,
                call.Span,
                $"Function '{call.FunctionId}' is not declared."
            );
            return;
        }

        ValidateCallShape(
            program,
            call.Destination,
            call.ReturnType,
            call.Arguments,
            function.Name,
            function.ReturnType,
            [ .. function.Parameters.Select(static parameter => parameter.Type) ],
            call.Span,
            diagnostics
        );
    }

    private static void ValidateUserCall(
        IrProgram program,
        IrInstruction.UserCall call,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrFunction? function = program.UserFunctions.FirstOrDefault(
            candidate => candidate.Id == call.FunctionId
        );

        if (function is null)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.UndefinedUserFunction,
                call.Span,
                $"User function '{call.FunctionId}' is not declared."
            );
            return;
        }

        IReadOnlyList<TypeSymbol> parameterTypes =
        [
            .. function.Slots
                .Where(static slot => slot.Kind == IrSlotKind.Parameter)
                .Select(static slot => slot.Type),
        ];

        ValidateCallShape(
            program,
            call.Destination,
            call.ReturnType,
            call.Arguments,
            function.Id,
            function.ReturnType,
            parameterTypes,
            call.Span,
            diagnostics
        );
    }

    private static void ValidateCallShape(
        IrProgram program,
        int? destination,
        TypeSymbol returnType,
        IReadOnlyList<int> arguments,
        string functionName,
        TypeSymbol expectedReturnType,
        IReadOnlyList<TypeSymbol> parameterTypes,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (
            returnType.Kind == TypeKind.Void &&
            destination is not null ||
            returnType.Kind != TypeKind.Void &&
            destination is null
        )
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                "IR call destination does not match the function return type."
            );
        }

        if (!TypeRelations.AreEquivalent(returnType, expectedReturnType))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR call return type does not match function '{functionName}'."
            );
        }

        if (destination is not null)
        {
            ValidateSlotType(
                program,
                destination.Value,
                expectedReturnType,
                span,
                diagnostics
            );
        }

        if (arguments.Count != parameterTypes.Count)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR call to '{functionName}' has an invalid argument count."
            );
            return;
        }

        for (int index = 0; index < arguments.Count; index++)
        {
            ValidateSlotAssignable(
                program,
                arguments[index],
                parameterTypes[index],
                span,
                diagnostics
            );
        }
    }

    private static void ValidateSlotAssignable(
        IrProgram program,
        int slotId,
        TypeSymbol expectedType,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? slot = GetSlot(program, slotId);

        if (slot is null)
        {
            ValidateSlot(program, slotId, span, diagnostics);
            return;
        }

        if (!TypeRelations.IsAssignable(slot.Type, expectedType))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR slot {slotId} has type '{slot.Type.DisplayName}', which is not assignable to '{expectedType.DisplayName}'."
            );
        }
    }

    private static void ValidateDefinitions(
        IrProgram program,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (!blocksById.ContainsKey(program.EntryBlock))
        {
            return;
        }

        IReadOnlyCollection<int> reachable = GetReachableBlocks(program.EntryBlock, blocksById);
        IReadOnlyDictionary<int, IReadOnlyList<int>> predecessors = GetPredecessors(reachable, blocksById);
        IReadOnlySet<int> allSlots = new HashSet<int>(program.Slots.Select(static slot => slot.Id));
        IReadOnlySet<int> parameterSlots = new HashSet<int>(
            program.Slots
                .Where(static slot => slot.Kind == IrSlotKind.Parameter)
                .Select(static slot => slot.Id)
        );
        Dictionary<int, IReadOnlySet<int>> outgoing = [ ];

        foreach (int blockId in reachable)
        {
            IReadOnlySet<int> initialDefinitions =
                blockId == program.EntryBlock
                    ? parameterSlots
                    : new HashSet<int>(allSlots);
            outgoing.Add(
                blockId,
                initialDefinitions
            );
        }

        bool changed;

        do
        {
            changed = false;

            foreach (int blockId in reachable.Order())
            {
                IReadOnlySet<int> incoming = GetIncomingDefinitions(
                    blockId,
                    program.EntryBlock,
                    predecessors,
                    outgoing,
                    parameterSlots
                );
                IReadOnlySet<int> definitions = ApplyDefinitions(
                    blocksById[blockId],
                    incoming
                );

                if (!outgoing[blockId].SetEquals(definitions))
                {
                    outgoing[blockId] = definitions;
                    changed = true;
                }
            }
        }
        while (changed);

        foreach (int blockId in reachable)
        {
            IReadOnlySet<int> defined = GetIncomingDefinitions(
                blockId,
                program.EntryBlock,
                predecessors,
                outgoing,
                parameterSlots
            );
            ValidateUses(blocksById[blockId], defined, diagnostics);
        }
    }

    private static IReadOnlySet<int> GetReachableBlocks(
        int entryBlock,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById
    )
    {
        HashSet<int> reachable = [ ];
        Queue<int> pending = new ();
        pending.Enqueue(entryBlock);

        while (pending.Count > 0)
        {
            int blockId = pending.Dequeue();

            if (
                reachable.Contains(blockId) ||
                !blocksById.TryGetValue(blockId, out IrBasicBlock? block)
            )
            {
                continue;
            }

            reachable.Add(blockId);

            foreach (int successor in GetSuccessors(block.Terminator))
            {
                pending.Enqueue(successor);
            }
        }

        return reachable;
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<int>> GetPredecessors(
        IReadOnlyCollection<int> reachable,
        IReadOnlyDictionary<int, IrBasicBlock> blocksById
    )
    {
        Dictionary<int, IReadOnlyList<int>> predecessors = reachable.ToDictionary(
            static blockId => blockId,
            static IReadOnlyList<int> (_) => new List<int>()
        );

        foreach (int blockId in reachable)
        {
            foreach (int successor in GetSuccessors(blocksById[blockId].Terminator))
            {
                if (predecessors.TryGetValue(successor, out IReadOnlyList<int>? values))
                {
                    ((IList<int>)values).Add(blockId);
                }
            }
        }

        return predecessors;
    }

    private static IReadOnlySet<int> GetIncomingDefinitions(
        int blockId,
        int entryBlock,
        IReadOnlyDictionary<int, IReadOnlyList<int>> predecessors,
        IReadOnlyDictionary<int, IReadOnlySet<int>> outgoing,
        IReadOnlySet<int> entryDefinitions
    )
    {
        if (blockId == entryBlock)
        {
            return entryDefinitions;
        }

        if (predecessors[blockId].Count == 0)
        {
            return ReadOnlySet<int>.Empty;
        }

        HashSet<int> incoming = [ .. outgoing[predecessors[blockId][0]] ];

        foreach (int predecessor in predecessors[blockId].Skip(1))
        {
            incoming.IntersectWith(outgoing[predecessor]);
        }

        return incoming;
    }

    private static IReadOnlySet<int> ApplyDefinitions(
        IrBasicBlock block,
        IEnumerable<int> incoming
    )
    {
        HashSet<int> defined = [ .. incoming ];

        foreach (IrInstruction instruction in block.Instructions)
        {
            int? destination = GetDestination(instruction);

            if (destination is not null)
            {
                defined.Add(destination.Value);
            }
        }

        return defined;
    }

    private static void ValidateUses(
        IrBasicBlock block,
        IEnumerable<int> defined,
        ICollection<Diagnostic> diagnostics
    )
    {
        ISet<int> allDefined = new HashSet<int>(defined);

        foreach (IrInstruction instruction in block.Instructions)
        {
            foreach (int operand in GetOperands(instruction))
            {
                if (!allDefined.Contains(operand))
                {
                    Report(
                        diagnostics,
                        IrDiagnosticCodes.UseBeforeDefinition,
                        instruction.Span,
                        $"IR slot {operand} is used before it is defined."
                    );
                }
            }

            int? destination = GetDestination(instruction);

            if (destination is not null)
            {
                allDefined.Add(destination.Value);
            }
        }

        foreach (int operand in GetOperands(block.Terminator))
        {
            if (!allDefined.Contains(operand))
            {
                Report(
                    diagnostics,
                    IrDiagnosticCodes.UseBeforeDefinition,
                    block.Terminator.Span,
                    $"IR slot {operand} is used before it is defined."
                );
            }
        }
    }

    private static int? GetDestination(IrInstruction instruction)
    {
        return instruction switch
        {
            IrInstruction.Constant value => value.Destination,
            IrInstruction.Copy copy => copy.Destination,
            IrInstruction.LoadGlobal global => global.Destination,
            IrInstruction.Unary unary => unary.Destination,
            IrInstruction.Binary binary => binary.Destination,
            IrInstruction.Convert conversion => conversion.Destination,
            IrInstruction.Truthiness truthiness => truthiness.Destination,
            IrInstruction.TypeTest typeTest => typeTest.Destination,
            IrInstruction.IsNull isNull => isNull.Destination,
            IrInstruction.HasProperty propertyTest => propertyTest.Destination,
            IrInstruction.CreateArray array => array.Destination,
            IrInstruction.CreateObject objectValue => objectValue.Destination,
            IrInstruction.GetProperty property => property.Destination,
            IrInstruction.GetElement element => element.Destination,
            IrInstruction.ProviderCall call => call.Destination,
            IrInstruction.UserCall call => call.Destination,
            _ => null,
        };
    }

    private static IEnumerable<int> GetOperands(IrInstruction instruction)
    {
        return instruction switch
        {
            IrInstruction.Copy copy => [ copy.Source ],
            IrInstruction.Unary unary => [ unary.Operand ],
            IrInstruction.Binary binary => [ binary.Left, binary.Right ],
            IrInstruction.Convert conversion => [ conversion.Source ],
            IrInstruction.Truthiness truthiness => [ truthiness.Source ],
            IrInstruction.TypeTest typeTest => [ typeTest.Source ],
            IrInstruction.IsNull isNull => [ isNull.Source ],
            IrInstruction.HasProperty propertyTest =>
                [ propertyTest.Target, propertyTest.Key ],
            IrInstruction.CreateArray array => array.Elements,
            IrInstruction.CreateObject objectValue =>
                objectValue.Properties.Select(static property => property.Value),
            IrInstruction.GetProperty property => [ property.Target ],
            IrInstruction.SetProperty property => [ property.Target, property.Value ],
            IrInstruction.RemoveProperty property => [ property.Target ],
            IrInstruction.GetElement element => [ element.Target, element.Index ],
            IrInstruction.SetElement element =>
                [ element.Target, element.Index, element.Value ],
            IrInstruction.RemoveElementProperty property =>
                [ property.Target, property.Key ],
            IrInstruction.ProviderCall call => call.Arguments,
            IrInstruction.UserCall call => call.Arguments,
            _ => [ ],
        };
    }

    private static IEnumerable<int> GetOperands(IrTerminator terminator)
    {
        return terminator switch
        {
            IrTerminator.Branch branch => [ branch.Condition ],
            IrTerminator.Return { Value: { } value } => [ value ],
            _ => [ ],
        };
    }

    private static IEnumerable<int> GetSuccessors(IrTerminator terminator)
    {
        return terminator switch
        {
            IrTerminator.Jump jump => [ jump.TargetBlock ],
            IrTerminator.Branch branch => [ branch.TrueBlock, branch.FalseBlock ],
            _ => [ ],
        };
    }

    private static void ValidateEquivalentSlots(
        IrProgram program,
        int destination,
        int source,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? destinationSlot = GetSlot(program, destination);
        IrSlot? sourceSlot = GetSlot(program, source);

        if (destinationSlot is null || sourceSlot is null)
        {
            ValidateSlot(program, destination, span, diagnostics);
            ValidateSlot(program, source, span, diagnostics);
            return;
        }

        if (!TypeRelations.AreEquivalent(destinationSlot.Type, sourceSlot.Type))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR cannot copy '{sourceSlot.Type.DisplayName}' into '{destinationSlot.Type.DisplayName}'."
            );
        }
    }

    private static void ValidateSlotType(
        IrProgram program,
        int slotId,
        TypeSymbol expectedType,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        IrSlot? slot = GetSlot(program, slotId);

        if (slot is null)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidSlot,
                span,
                $"IR slot {slotId} does not exist."
            );
            return;
        }

        if (!TypeRelations.AreEquivalent(slot.Type, expectedType))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.TypeMismatch,
                span,
                $"IR slot {slotId} has type '{slot.Type.DisplayName}', expected '{expectedType.DisplayName}'."
            );
        }
    }

    private static void ValidateSlot(
        IrProgram program,
        int slotId,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (GetSlot(program, slotId) is null)
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidSlot,
                span,
                $"IR slot {slotId} does not exist."
            );
        }
    }

    private static IrSlot? GetSlot(IrProgram program, int slotId)
    {
        return slotId >= 0 && slotId < program.Slots.Count
            ? program.Slots[slotId]
            : null;
    }

    private static void ValidateBlockTarget(
        IReadOnlyDictionary<int, IrBasicBlock> blocksById,
        int target,
        TextSpan span,
        ICollection<Diagnostic> diagnostics
    )
    {
        if (!blocksById.ContainsKey(target))
        {
            Report(
                diagnostics,
                IrDiagnosticCodes.InvalidStructure,
                span,
                $"IR block target {target} does not exist."
            );
        }
    }

    private static void Report(
        ICollection<Diagnostic> diagnostics,
        string code,
        TextSpan span,
        string message
    )
    {
        diagnostics.Add(
            new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticCategory.Exporter,
                span,
                message
            )
        );
    }
}
