using MuLang.Core.Text;
using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal sealed class IrAttributeBuilder
{
    private readonly IDictionary<string, IrFunctionAttributes> functions =
        new Dictionary<string, IrFunctionAttributes>(StringComparer.Ordinal);

    private readonly IDictionary<IrSlotAttributeTarget, IrSlotAttributes> slots =
        new Dictionary<IrSlotAttributeTarget, IrSlotAttributes>();

    private readonly IDictionary<string, string> providerGlobals =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly IDictionary<string, string> providerFunctions =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public void AddFunction(
        string functionId,
        string sourceName,
        TextSpan declarationSpan
    )
    {
        functions[functionId] = new IrFunctionAttributes(
            sourceName,
            null,
            declarationSpan
        );
    }

    public void AddSlot(
        string functionId,
        int slotId,
        string sourceName,
        TextSpan declarationSpan
    )
    {
        slots[new IrSlotAttributeTarget(functionId, slotId)] =
            new IrSlotAttributes(sourceName, declarationSpan);
    }

    public void AddProviderGlobal(string id, string name)
    {
        providerGlobals[id] = name;
    }

    public void AddProviderFunction(string id, string name)
    {
        providerFunctions[id] = name;
    }

    public IrAttributeCollection Build()
    {
        IrProgramAttributes? program =
            providerGlobals.Count == 0 && providerFunctions.Count == 0
                ? null
                : new IrProgramAttributes(
                    null,
                    [
                        .. providerGlobals
                            .OrderBy(static item => item.Key, StringComparer.Ordinal)
                            .Select(
                                static item => new IrProviderSymbolName(
                                    item.Key,
                                    item.Value
                                )
                            ),
                    ],
                    [
                        .. providerFunctions
                            .OrderBy(static item => item.Key, StringComparer.Ordinal)
                            .Select(
                                static item => new IrProviderSymbolName(
                                    item.Key,
                                    item.Value
                                )
                            ),
                    ]
                );

        return new IrAttributeCollection(
            program,
            functions,
            slots,
            []
        );
    }
}
