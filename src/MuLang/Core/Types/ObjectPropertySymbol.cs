namespace MuLang.Core.Types;

public sealed class ObjectPropertySymbol
{
    public ObjectPropertySymbol(string name, TypeSymbol type, bool isOptional = false)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (type.Kind is TypeKind.Void or TypeKind.Null or TypeKind.Error)
        {
            throw new ArgumentException(
                $"Type '{type.DisplayName}' cannot be used for an object property.",
                nameof(type)
            );
        }

        Name = name;
        Type = type;
        IsOptional = isOptional;
    }

    public string Name { get; }

    public TypeSymbol Type { get; }

    public bool IsOptional { get; }
}
