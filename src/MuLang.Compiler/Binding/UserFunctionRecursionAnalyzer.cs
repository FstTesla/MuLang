namespace MuLang.Compiler.Binding;

internal static class UserFunctionRecursionAnalyzer
{
    public static IReadOnlyCollection<string> FindRecursiveFunctions(
        IReadOnlyCollection<string> functionIds,
        IReadOnlyDictionary<string, ISet<string>> calls
    )
    {
        IDictionary<string, int> indices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        IDictionary<string, int> lowLinks =
            new Dictionary<string, int>(StringComparer.Ordinal);
        ISet<string> onStack = new HashSet<string>(StringComparer.Ordinal);
        Stack<string> stack = [ ];
        ISet<string> recursiveFunctions = new HashSet<string>(
            StringComparer.Ordinal
        );
        int nextIndex = 0;

        foreach (string functionId in functionIds)
        {
            if (!indices.ContainsKey(functionId))
            {
                Visit(functionId);
            }
        }

        return [ .. recursiveFunctions.Order(StringComparer.Ordinal) ];

        void Visit(string functionId)
        {
            int index = nextIndex++;
            indices.Add(functionId, index);
            lowLinks.Add(functionId, index);
            stack.Push(functionId);
            onStack.Add(functionId);

            if (calls.TryGetValue(functionId, out ISet<string>? targets))
            {
                foreach (string target in targets)
                {
                    if (!indices.TryGetValue(target, out int targetIndex))
                    {
                        Visit(target);
                        lowLinks[functionId] = Math.Min(
                            lowLinks[functionId],
                            lowLinks[target]
                        );
                    }
                    else if (onStack.Contains(target))
                    {
                        lowLinks[functionId] = Math.Min(
                            lowLinks[functionId],
                            targetIndex
                        );
                    }
                }
            }

            if (lowLinks[functionId] != indices[functionId])
            {
                return;
            }

            IList<string> component = [ ];
            string member;

            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != functionId);

            bool hasSelfEdge =
                component.Count == 1 &&
                calls.TryGetValue(functionId, out ISet<string>? selfTargets) &&
                selfTargets.Contains(functionId);

            if (component.Count > 1 || hasSelfEdge)
            {
                recursiveFunctions.UnionWith(component);
            }
        }
    }
}
