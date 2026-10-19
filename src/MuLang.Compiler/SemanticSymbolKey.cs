using System.Runtime.CompilerServices;

namespace MuLang.Compiler;

/// <summary>Represents the identity of a resolved semantic symbol.</summary>
public sealed class SemanticSymbolKey : IEquatable<SemanticSymbolKey>
{
    private readonly object identity;
    private readonly string? workspaceIdentity;

    internal SemanticSymbolKey(
        SemanticClassificationKind kind,
        object identity,
        string? workspaceIdentity = null
    )
    {
        Kind = kind;
        this.identity = identity;
        this.workspaceIdentity = workspaceIdentity;
    }

    /// <summary>Gets the semantic symbol kind.</summary>
    public SemanticClassificationKind Kind { get; }

    /// <summary>Gets a value indicating whether independently analyzed documents can contain the same symbol.</summary>
    public bool IsWorkspaceStable => workspaceIdentity is not null;

    /// <inheritdoc />
    public bool Equals(SemanticSymbolKey? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Kind != other.Kind)
        {
            return false;
        }

        return workspaceIdentity is not null && other.workspaceIdentity is not null
            ? string.Equals(
                workspaceIdentity,
                other.workspaceIdentity,
                StringComparison.Ordinal
            )
            : ReferenceEquals(identity, other.identity);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is SemanticSymbolKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return workspaceIdentity is null
            ? HashCode.Combine(Kind, RuntimeHelpers.GetHashCode(identity))
            : HashCode.Combine(
                Kind,
                StringComparer.Ordinal.GetHashCode(workspaceIdentity)
            );
    }
}
