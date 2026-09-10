using System.Runtime.CompilerServices;

namespace MuLang.Exporters.DotNet;

internal sealed class ReferencePairComparer : IEqualityComparer<ReferencePair>
{
    public static ReferencePairComparer Instance { get; } = new();

    public bool Equals(ReferencePair x, ReferencePair y)
    {
        return ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);
    }

    public int GetHashCode(ReferencePair pair)
    {
        return HashCode.Combine(
            RuntimeHelpers.GetHashCode(pair.Left),
            RuntimeHelpers.GetHashCode(pair.Right)
        );
    }
}
