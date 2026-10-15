namespace MuLang.LanguageServer;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            string? fallbackWorkspaceFolder = null;
            bool useVisualStudioClassifications = false;

            foreach (string argument in args)
            {
                if (argument == "--visual-studio")
                {
                    useVisualStudioClassifications = true;
                    continue;
                }

                const string workspaceRootPrefix = "--workspace-root=";

                if (
                    argument.StartsWith(
                        workspaceRootPrefix,
                        StringComparison.Ordinal
                    )
                )
                {
                    string workspaceRoot = argument[
                        workspaceRootPrefix.Length..
                    ];

                    if (
                        fallbackWorkspaceFolder is not null ||
                        !Uri.TryCreate(
                            workspaceRoot,
                            UriKind.Absolute,
                            out Uri? workspaceRootUri
                        ) ||
                        !workspaceRootUri.IsFile
                    )
                    {
                        throw new ArgumentException(
                            $"Invalid workspace root '{workspaceRoot}'.",
                            nameof(args)
                        );
                    }

                    fallbackWorkspaceFolder = workspaceRootUri.AbsoluteUri;
                    continue;
                }

                throw new ArgumentException(
                    $"Unsupported command-line argument '{argument}'.",
                    nameof(args)
                );
            }

            LanguageServer server = new (
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                useVisualStudioClassifications,
                fallbackWorkspaceFolder
            );

            return await server.RunAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.ToString());

            return 1;
        }
    }
}
