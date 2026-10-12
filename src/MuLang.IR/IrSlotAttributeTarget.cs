namespace MuLang.IR;

/// <summary>Identifies a function-local IR slot attribute target.</summary>
/// <param name="FunctionId">The containing function identifier.</param>
/// <param name="SlotId">The function-local slot identifier.</param>
public readonly record struct IrSlotAttributeTarget(string FunctionId, int SlotId);
